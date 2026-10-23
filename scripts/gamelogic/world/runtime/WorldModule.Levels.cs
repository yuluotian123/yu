using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Framework;
using Framework.UI;
using Godot;
using GameLogic.AI;
namespace GameLogic;

public sealed partial class WorldModule
{
    Node3D _sessionRoot,_actorsRoot,_levelsRoot;
    GameObject3D _activeLevel;
    SceneHandle _activeHandle,_playerHandle,_loading;
    IUIModule _loadingUi;
    float _loadProgress,_loadWeight;
    CancellationTokenSource _transition;
    int _operation;
    bool _committing,_ownsLevel;
    bool? _pausedBeforeTransition;
    ulong _arrivalUntil;
    internal bool ArrivalBlocked=>Time.GetTicksMsec()<_arrivalUntil;
    public double LastTransitionMilliseconds {get;private set;}
    public async Task<bool> OpenAsync(Node parent,WorldDefinition definition,bool resume=true,string slot="default",CancellationToken cancellation=default)
    {
        Close();Configure(definition);_objects.Clear();_resumeLevel="";_resumeTransform=null;_restoreRequested=false;
        _save.ResetSession();ModuleSystem.GetModule<ITimeOfDayModule>().ResetCalendar();
        if(resume&&_save.Exists(slot)&&!_save.Load(slot)){LastError="存档无法读取。";return false;}
        WorldCatalog.Require(GodotObject.IsInstanceValid(parent)&&parent.IsInsideTree(),"世界宿主不在场景树中。");
        CreateSession(parent);
        string level=_resumeLevel.Length>0?_resumeLevel:definition.InitialLevel?.Id;
        Task<bool> request=SwitchInternalAsync(level,"",_resumeTransform!=null,cancellation);
        int operation=_operation;
        bool success=await request;
        if(!success&&operation==_operation)Close();
        return success;
    }
    void CreateSession(Node parent)
    {
        _sessionRoot=new(){Name="WorldSessionRoot"};_actorsRoot=new(){Name="Actors"};_levelsRoot=new(){Name="Levels"};
        _sessionRoot.AddChild(_actorsRoot);_sessionRoot.AddChild(_levelsRoot);parent.AddChild(_sessionRoot);
        if(_definition.Calendar!=null)ModuleSystem.GetModule<ITimeOfDayModule>().Attach(_sessionRoot,_definition.Calendar);
        Population?.Start(_sessionRoot);
    }
    internal void AdoptStandalone(WorldLevelContextComponent3D context)
    {
        if(IsOpen||!GodotObject.IsInstanceValid(context.Owner)||!context.Owner.IsInsideTree())return;
        CreateSession(context.Owner.GetTree().Root);_activeLevel=context.Owner;_ownsLevel=false;
        CurrentLevelId=context.Level.Id;CurrentContext=context;Status="关卡运行中";
        _ = CreateStandalonePlayer(context);
    }
    async Task CreateStandalonePlayer(WorldLevelContextComponent3D context)
    {
        int operation=_operation;
        var handle=ModuleSystem.GetModule<IResourceModule>().LoadSceneAsync(_definition.PlayerScene);
        try
        {
            await handle.Task;if(operation!=_operation||!IsOpen)return;
            if(!handle.IsValid)throw new InvalidOperationException(handle.Error);
            var player=InstantiateHost(handle,"玩家场景必须使用 GameObject3D。");
            Player=player;_actorsRoot.AddChild(player);_playerHandle=handle;handle=null;
            PlacePlayer(FindEntry(context.Owner,context.Level.DefaultEntry));
        }
        catch(Exception error){LastError=error.Message;GD.PushError("独立关卡玩家初始化失败："+error.Message);}
        finally{handle?.Dispose();}
    }
    public Task<bool> SwitchAsync(string levelId,string entry="",CancellationToken cancellation=default)=>SwitchInternalAsync(levelId,entry,false,cancellation);
    public void CancelTransition(){if(!_committing)_transition?.Cancel();}
    async Task<bool> SwitchInternalAsync(string levelId,string entry,bool restorePosition,CancellationToken cancellation)
    {
        if(!IsOpen){LastError="世界会话未打开。";return false;}
        if(Population?.IsCatchingUp==true){LastError="正在补算世界事件。";return false;}
        if(IsSwitching){LastError="已有正在执行的关卡切换。";return false;}
        if(levelId==null||Catalog?.Levels.TryGetValue(levelId,out var target)!=true){LastError="目标关卡未登记。";return false;}
        var operation=++_operation;
        var source=CancellationTokenSource.CreateLinkedTokenSource(cancellation);_transition=source;var token=source.Token;
        SceneHandle nextHandle=null,newPlayerHandle=null;
        GameObject3D next=null,old=_activeLevel;
        var oldContext=CurrentContext;var oldLevel=CurrentLevelId;var oldTransform=Player?.GlobalTransform;
        bool oldOwns=_ownsLevel,detached=false,success=false;
        var playerProcess=Player?.ProcessMode??Node.ProcessModeEnum.Inherit;
        bool oldClockPaused=ModuleSystem.GetModule<ITimeOfDayModule>().Clock.Paused;
        Dictionary<string,JsonObject> beforeObjects=null;
        var timer=Stopwatch.StartNew();LastError="";Status="准备加载";_loadProgress=0;_loadWeight=0;
        try
        {
            token.ThrowIfCancellationRequested();
            if(GodotObject.IsInstanceValid(Player))Player.ProcessMode=Node.ProcessModeEnum.Disabled;
            var transitionTree=_sessionRoot.GetTree();
            _loadingUi.ShowUI<LevelLoadingWindow>(this,string.IsNullOrWhiteSpace(target.DisplayName)?target.ScenePath.GetFile().GetBaseName():target.DisplayName);
            // Let the overlay attach and draw before cached loads can instantiate a large scene.
            do
            {
                await transitionTree.ToSignal(transitionTree,SceneTree.SignalName.ProcessFrame);CheckRequest(operation,token);
            }while(_loadingUi.GetWindow<LevelLoadingWindow>()?.IsLoadDone!=true);
            var loadingWindow=_loadingUi.GetWindow<LevelLoadingWindow>();
            if(!GodotObject.IsInstanceValid(loadingWindow.Owner))throw new InvalidOperationException("加载界面不可用。");
            await transitionTree.ToSignal(transitionTree,SceneTree.SignalName.ProcessFrame);CheckRequest(operation,token);
            Status="加载关卡";_loadProgress=.05f;_loadWeight=.65f;
            nextHandle=ModuleSystem.GetModule<IResourceModule>().LoadSceneAsync(target.ScenePath).WithCancellation(token);
            _loading=nextHandle;await nextHandle.Task;CheckRequest(operation,token);
            if(!nextHandle.IsValid)throw new InvalidOperationException("关卡加载失败："+nextHandle.Error);
            _loading=null;_loadProgress=.7f;_loadWeight=0;Status="创建关卡";
            next=InstantiateHost(nextHandle,"关卡根必须使用 GameObject3D。");
            var context=next.Components.OfType<WorldLevelContextComponent3D>().SingleOrDefault()
                ??throw new InvalidOperationException("关卡缺少唯一 WorldLevelContextComponent3D。");
            context=(WorldLevelContextComponent3D)context.Duplicate();context.Level=target;
            for(int i=0;i<next.Components.Count;i++)if(next.Components[i] is WorldLevelContextComponent3D)next.Components[i]=context;
            string arrival=string.IsNullOrWhiteSpace(entry)?target.DefaultEntry:entry;
            WorldIdentity.Bind(next);ValidateScene(next,target,arrival);
            while(Population?.Backlog>0)
            {Population.Process(true);await _sessionRoot.ToSignal(_sessionRoot.GetTree(),SceneTree.SignalName.ProcessFrame);CheckRequest(operation,token);}
            Population?.PrepareScene(next,target);
            if(Player==null)
            {
                Status="加载玩家";_loadProgress=.75f;_loadWeight=.1f;
                newPlayerHandle=ModuleSystem.GetModule<IResourceModule>().LoadSceneAsync(_definition.PlayerScene).WithCancellation(token);
                _loading=newPlayerHandle;
                await newPlayerHandle.Task;CheckRequest(operation,token);
                if(!newPlayerHandle.IsValid)throw new InvalidOperationException("玩家场景加载失败："+newPlayerHandle.Error);
            }
            _loading=null;_loadProgress=.85f;_loadWeight=0;
            // Cached loads may complete inside an Area3D physics callback. Commit outside the query flush.
            await transitionTree.ToSignal(transitionTree,SceneTree.SignalName.ProcessFrame);
            CheckRequest(operation,token);_committing=true;Status="交接关卡";_loadProgress=.9f;
            ModuleSystem.GetModule<ITimeOfDayModule>().Clock.Paused=true;
            _pausedBeforeTransition=oldClockPaused;
            if(Player!=null)Player.ProcessMode=Node.ProcessModeEnum.Disabled;
            _save.CheckpointSections();beforeObjects=_objects.ToDictionary(p=>p.Key,p=>p.Value.Capture());
            foreach(var environment in _environments.Values.ToArray())foreach(var actor in environment.Actors.Values.ToArray())if(actor.ManagedActor==null)actor.CancelPlan("LevelChanging");
            if(old!=null)
            {
                old.GetParent().RemoveChild(old);detached=true;
            }
            _activeLevel=next;_levels[next.GetInstanceId()]=(target,null);
            _levelsRoot.AddChild(next);CurrentContext=next.GetComponent<WorldLevelContextComponent3D>();
            ValidateActivation(next);
            if(Player==null)
            {
                Player=InstantiateHost(newPlayerHandle,"玩家场景必须使用 GameObject3D。");
                Player.ProcessMode=Node.ProcessModeEnum.Disabled;_actorsRoot.AddChild(Player);ValidateActivation(Player);
                _playerHandle=newPlayerHandle;newPlayerHandle=null;
            }
            PlacePlayer(restorePosition&&_resumeTransform is {} saved?saved:FindEntry(next,arrival));
            Status="等待物理与导航";_loadProgress=.95f;
            var tree=next.GetTree();await tree.ToSignal(tree,SceneTree.SignalName.PhysicsFrame);CheckRequest(operation,token);
            if(CurrentContext?.RequireNavigation==true)
            {
                var navigation=next.GetNodeOrNull<NavigationRegion3D>(CurrentContext.NavigationRegionPath)
                    ??throw new InvalidOperationException("关卡导航区域缺失。");
                long started=(long)Time.GetTicksMsec();
                while(NavigationServer3D.MapGetIterationId(navigation.GetNavigationMap())==0)
                {
                    if((long)Time.GetTicksMsec()-started>5000)throw new InvalidOperationException("关卡导航准备超时。");
                    await tree.ToSignal(tree,SceneTree.SignalName.PhysicsFrame);CheckRequest(operation,token);
                }
            }
            Status="加载完成";_loadProgress=1;loadingWindow.RefreshProgress();
            // Present the completed bar before revealing the ready level.
            for(int frame=0;frame<2;frame++)
            {await transitionTree.ToSignal(transitionTree,SceneTree.SignalName.ProcessFrame);CheckRequest(operation,token);}
            CurrentLevelId=target.Id;_activeHandle?.Dispose();_activeHandle=nextHandle;nextHandle=null;
            _ownsLevel=true;_resumeLevel="";_resumeTransform=null;_restoreRequested=false;
            if(old!=null&&GodotObject.IsInstanceValid(old))old.Free();
            Status="关卡运行中";success=true;return true;
        }
        catch(Exception error)
        {
            if(operation!=_operation)return false;
            LastError=error is OperationCanceledException?"关卡切换已取消。":error.Message;Status="切换失败";
            if(detached)
            {
                if(GodotObject.IsInstanceValid(next)){if(next.GetParent()!=null)next.GetParent().RemoveChild(next);_levels.Remove(next.GetInstanceId());next.Free();next=null;}
                if(beforeObjects!=null){_objects.Clear();foreach(var pair in beforeObjects)_objects[pair.Key]=WorldObjectState.Read(pair.Value,DefinitionsFor(pair.Key));}
                _activeLevel=old;CurrentLevelId=oldLevel;_ownsLevel=oldOwns;CurrentContext=oldContext;
                if(GodotObject.IsInstanceValid(old))
                {
                    foreach(var host in Hosts(old))host.RequestReady();
                    _levels[old.GetInstanceId()]=(Catalog.Levels[oldLevel],null);_levelsRoot.AddChild(old);
                    CurrentContext=old.GetComponent<WorldLevelContextComponent3D>();
                    if(oldTransform is {} transform)PlacePlayer(transform);
                }
            }
            return false;
        }
        finally
        {
            if(!success&&GodotObject.IsInstanceValid(next)&&next!=_activeLevel){_levels.Remove(next.GetInstanceId());next.Free();}
            nextHandle?.Dispose();newPlayerHandle?.Dispose();
            if(operation==_operation)
            {
                _loading=null;_committing=false;
                _loadingUi.CloseUI<LevelLoadingWindow>();
                ModuleSystem.GetModule<ITimeOfDayModule>().Clock.Paused=oldClockPaused;
                _pausedBeforeTransition=null;
                if(GodotObject.IsInstanceValid(Player))Player.ProcessMode=playerProcess;
                if(ReferenceEquals(_transition,source))_transition=null;
                LastTransitionMilliseconds=timer.Elapsed.TotalMilliseconds;
            }
            else if(detached&&GodotObject.IsInstanceValid(old))old.Free();
            source.Dispose();
        }
    }
    void CheckRequest(int operation,CancellationToken cancellation)
    {cancellation.ThrowIfCancellationRequested();if(operation!=_operation||!IsOpen)throw new OperationCanceledException();}
    static GameObject3D InstantiateHost(SceneHandle handle,string error)
    {
        var node=handle.Instantiate();if(node is GameObject3D host)return host;
        if(GodotObject.IsInstanceValid(node))node.Free();throw new InvalidOperationException(error);
    }
    internal static IEnumerable<GameObject3D> Hosts(Node root)
    {
        if(root is GameObject3D host)yield return host;
        using var children=(Godot.Collections.Array)root.GetChildren();foreach(Variant child in children)foreach(var descendant in Hosts(child.As<Node>()))yield return descendant;
    }
    void ValidateScene(GameObject3D root,WorldLevelDefinition level,string entry)
    {
        var identities=new HashSet<string>();var entries=new HashSet<string>();var pointIds=new HashSet<string>();
        foreach(var host in Hosts(root))
        {
            if(host.PersistentId.Length>0)WorldCatalog.Require(host.PersistentId!="player"&&identities.Add(host.PersistentId),"关卡含玩家实例或重复宿主身份："+host.PersistentId);
            foreach(var spawn in host.Components.OfType<WorldSpawnPointComponent3D>())
            {
                WorldCatalog.Require(spawn.Entry.Length>0&&entries.Add(spawn.Entry),"出生点身份为空或重复。");
                WorldCatalog.Require(spawn.MarkerPath.IsEmpty||host.GetNodeOrNull<Node3D>(spawn.MarkerPath)!=null,"出生点标记缺失："+spawn.Entry);
            }
            var bindings=host.Components.OfType<WorldObjectComponent3D>().ToArray();
            WorldCatalog.Require(bindings.Length<=1,"一个宿主只能配置一个世界对象绑定。");
            if(host.Components.OfType<IWorldCapabilityProvider>().Any())WorldCatalog.Require(bindings.Length==1,"能力组件缺少世界对象绑定："+host.Name);
            foreach(var binding in bindings)
            {
                string id=host.PersistentId;
                WorldCatalog.Require(id.Length>0&&pointIds.Add(id),"世界对象身份为空或重复。");
                WorldCatalog.Require(binding.ObjectType!=null,"世界对象缺少类型。");
                WorldCatalog.ValidateCapabilities(binding.ObjectType.Capabilities);WorldCatalog.ValidateOverrides(binding);
                binding.ValidateComponents(host.Components,host);
                WorldCatalog.Require(Catalog.Objects.TryGetValue(id,out var catalogEntry)&&catalogEntry.Level.Id==level.Id,"世界目录与关卡不匹配，请保存关卡："+id);
            }
        }
        WorldCatalog.Require(entries.Contains(entry),"目标入口缺失："+entry);
    }
    static void ValidateActivation(GameObject3D root)
    {foreach(var host in Hosts(root))WorldCatalog.Require(host.InitializationError.Length==0,"组件初始化失败："+host.Name+" · "+host.InitializationError);}
    static Transform3D FindEntry(GameObject3D root,string entry)
    {
        foreach(var host in Hosts(root))
            if(host.GetComponent<WorldSpawnPointComponent3D>() is {} spawn&&spawn.Entry==entry)return spawn.Transform;
        throw new InvalidOperationException("目标入口未激活："+entry);
    }
    void PlacePlayer(Transform3D transform)
    {
        if(!GodotObject.IsInstanceValid(Player))return;
        Player.GlobalTransform=transform;
        _arrivalUntil=Time.GetTicksMsec()+750;
        var movement=Player.GetComponent<CharacterMovementComponent3D>();movement?.SyncBodyToOwner();if(movement!=null)movement.Velocity=Vector3.Zero;
        Player.GetComponent<NpcNavigationComponent3D>()?.Stop();Player.ResetPhysicsInterpolation();
        ModuleSystem.GetModule<ICameraModule>().Follow(Player,true);
    }
    public void Close()
    {
        ++_operation;_transition?.Cancel();_transition=null;_loading?.Dispose();_loading=null;_committing=false;_restoreRequested=false;
        _loadingUi?.CloseUI<LevelLoadingWindow>();
        if(_pausedBeforeTransition is {} paused){ModuleSystem.GetModule<ITimeOfDayModule>().Clock.Paused=paused;_pausedBeforeTransition=null;}
        if(IsOpen)_save?.CheckpointSections();
        Population?.Close();
        if(_ownsLevel&&GodotObject.IsInstanceValid(_activeLevel))_activeLevel.Free();
        if(GodotObject.IsInstanceValid(_sessionRoot))ModuleSystem.GetModule<ITimeOfDayModule>().Detach(_sessionRoot);
        if(GodotObject.IsInstanceValid(_sessionRoot))_sessionRoot.Free();
        _activeHandle?.Dispose();_playerHandle?.Dispose();_activeHandle=_playerHandle=null;
        _activeLevel=null;_sessionRoot=_actorsRoot=_levelsRoot=null;Player=null;CurrentContext=null;CurrentLevelId="";_ownsLevel=false;
        _objects.Clear();_localDefinitions.Clear();Status="未进入世界";
    }
}
