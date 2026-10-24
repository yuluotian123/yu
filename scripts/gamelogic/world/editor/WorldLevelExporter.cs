#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic;

/// <summary>Explicit scene-to-world authoring; subsequent saves refresh generated portal routes.</summary>
public static class WorldLevelExporter
{
    public static bool IsExporting {get;private set;}
    public static void ExportCurrent(WorldLevelContextComponent3D context)
    {
        try
        {
            var editor=EditorInterface.Singleton;
            var root=editor.GetEditedSceneRoot() as GameObject3D;
            WorldCatalog.Require(root!=null&&root.Components.Contains(context),"请在当前关卡根节点上使用导出按钮。");
            var world=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
            string report=Export(root,context,world,()=>
            {
                editor.MarkSceneAsUnsaved();
                WorldCatalog.Require(editor.SaveScene()==Error.Ok,"场景保存失败，未完成导出。");
            });
            editor.GetResourceFilesystem().Scan();
            GD.Print(report);
        }
        catch(Exception error){GD.PushError("关卡配置导出失败："+error.Message);}
    }

    public static string Export(GameObject3D root,WorldLevelContextComponent3D context,WorldDefinition world,Action saveScene)
    {
        WorldCatalog.Require(!IsExporting,"已有正在进行的关卡导出。");
        WorldCatalog.Require(root!=null&&root.Components.Contains(context)&&root.Components.OfType<WorldLevelContextComponent3D>().Count()==1,"关卡根节点需要唯一的 WorldLevelContextComponent3D。");
        WorldCatalog.Require(root.SceneFilePath.StartsWith("res://")&&FileAccess.FileExists(root.SceneFilePath),"请先将当前关卡保存为 Scene，再导出配置。");
        WorldCatalog.Require(world!=null&&world.ResourcePath.StartsWith("res://"),"世界配置尚未保存。");
        var entrances=WorldModule.Hosts(root).SelectMany(h=>h.Components.OfType<WorldSpawnPointComponent3D>().Select(s=>(Host:h,Spawn:s))).ToArray();
        WorldCatalog.Require(entrances.Length>0,"请先在关卡内放置出生点及 Arrival 标记。");
        WorldCatalog.Require(entrances.All(p=>!string.IsNullOrWhiteSpace(p.Spawn.Entry)&&p.Host.GetNodeOrNull<Node3D>(p.Spawn.MarkerPath)!=null)
            &&entrances.Select(p=>p.Spawn.Entry).Distinct(StringComparer.Ordinal).Count()==entrances.Length,"出生点标记缺失，或入口名称为空／重复。");
        var level=world.Levels.SingleOrDefault(l=>l.ScenePath==root.SceneFilePath)??context.Level;
        // Save As from an existing level must not retarget its source record.
        if(level==null||level.ScenePath!=root.SceneFilePath&&FileAccess.FileExists(level.ScenePath))level=new();
        string entry=level.DefaultEntry;
        if(!entrances.Any(p=>p.Spawn.Entry==entry))
        {
            WorldCatalog.Require(entrances.Length==1,"存在多个出生点，请在 Level.DefaultEntry 指定默认入口。");
            entry=entrances[0].Spawn.Entry;
        }
        using var working=new WorldDefinition{Id=world.Id,DisplayName=world.DisplayName,Locations=new(world.Locations),Levels=new(world.Levels),ObjectTypes=new(world.ObjectTypes),Routes=new(world.Routes),InitialLevel=world.InitialLevel,Calendar=world.Calendar,PlayerScene=world.PlayerScene};
        var portals=WorldModule.Hosts(root).SelectMany(h=>h.Components.OfType<WorldTransitionComponent3D>()).ToArray();
        foreach(var portal in portals)
        {
            WorldCatalog.Require(double.IsFinite(portal.NpcTravelHours)&&portal.NpcTravelHours>=0,"传送门的 NPC 旅行时长必须为有限非负数。");
            if(portal.Automatic&&portal.TargetLevel!=null)
                WorldCatalog.Require(portal.TargetLevel.Id==level.Id||world.Levels.Any(l=>l.Id==portal.TargetLevel.Id),"请先导出目标关卡："+portal.TargetLevel.DisplayName);
        }
        IsExporting=true;
        try
        {
            string folder=world.ResourcePath.GetBaseDir();
            level.ScenePath=root.SceneFilePath;level.DefaultEntry=entry;level.ExportPortalRoutes=true;
            if(string.IsNullOrWhiteSpace(level.DisplayName))level.DisplayName=root.Name;
            if(level.Location==null)
            {
                level.Location=new(){DisplayName=level.DisplayName};
                Save(level.Location,folder+"/locations/"+level.Location.Id+".tres");
            }
            AddLocation(working,level.Location);
            foreach(var binding in WorldModule.Hosts(root).SelectMany(h=>h.Components.OfType<WorldObjectComponent3D>()))
            {
                WorldCatalog.Require(binding.ObjectType!=null,"对象缺少类型："+binding.DisplayName);
                if(!working.ObjectTypes.Any(t=>t.Id==binding.ObjectType.Id))working.ObjectTypes.Add(binding.ObjectType);
                if(binding.Location!=null)AddLocation(working,binding.Location);
            }
            if(!working.Levels.Any(l=>l.Id==level.Id))working.Levels.Add(level);
            working.InitialLevel??=level;
            Save(level,level.ResourcePath.Length>0&&!level.ResourcePath.Contains("::",StringComparison.Ordinal)?level.ResourcePath:folder+"/levels/"+level.Id+".tres");
            if(context.Level!=level)
            {
                // A component inherited from another Scene may still share its Resource instance.
                var local=(WorldLevelContextComponent3D)context.Duplicate();local.Level=level;
                root.Components=new(root.Components.Select(c=>c==context?local:c));context=local;
            }
            context.EmitChanged();context.NotifyPropertyListChanged();root.NotifyPropertyListChanged();
            WorldAuthoringCompiler.Prepare(root,true);saveScene();
            WorldAuthoringCompiler.CompileAll(working);
            _=new WorldRoutes(new WorldCatalog(working));
            world.Levels=working.Levels;world.Locations=working.Locations;world.ObjectTypes=working.ObjectTypes;world.Routes=working.Routes;world.InitialLevel=working.InitialLevel;
            Save(world,world.ResourcePath);
            WorldAuthoringCompiler.RefreshEditorCatalog(world);
            var catalog=ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath);
            int generated=world.Routes.Count(r=>r.From.Id==level.Id&&r.SourcePortalId.Length>0);
            int unconfigured=portals.Count(p=>p.TargetLevel==null),manual=portals.Count(p=>!p.Automatic&&p.TargetLevel!=null);
            return $"关卡配置已导出：{level.DisplayName} · 入口 {catalog.Entrances.Count} · 对象 {catalog.Entries.Count} · NPC {catalog.Actors.Count} · 自动路线 {generated}。已有手工路线保留。"
                +(unconfigured>0?$" {unconfigured} 个门尚未配置目标，暂不生成路线。":"")
                +(manual>0?$" {manual} 个门由事件控制，未自动生成路线，避免绕过事件条件。":"");
        }
        finally{IsExporting=false;}
    }
    static void AddLocation(WorldDefinition world,WorldLocationDefinition location)
    {
        var visited=new System.Collections.Generic.HashSet<string>();
        for(var item=location;item!=null;item=item.Parent)
        {
            WorldCatalog.Require(visited.Add(item.Id),"地点父级存在循环："+item.DisplayName);
            if(!world.Locations.Any(l=>l.Id==item.Id))world.Locations.Add(item);
        }
    }
    static void Save(Resource resource,string path)
    {
        WorldCatalog.Require(DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(path.GetBaseDir()))==Error.Ok,"无法创建配置目录："+path);
        WorldCatalog.Require(ResourceSaver.Save(resource,path)==Error.Ok,"配置保存失败："+path);
        resource.TakeOverPath(path);
    }
    internal static Godot.Collections.Array<Godot.Collections.Dictionary> CapturePortals(Node root)
    {
        var result=new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach(var (host,id) in WorldIdentity.Resolve(root,true))
        {
            var portals=host.Components.OfType<WorldTransitionComponent3D>().ToArray();
            WorldCatalog.Require(portals.Length==1,"一个宿主只能配置一个传送门："+host.Name);
            var portal=portals[0];
            WorldCatalog.Require(double.IsFinite(portal.NpcTravelHours)&&portal.NpcTravelHours>=0,"NPC 旅行时长无效："+host.Name);
            Node3D marker=host.GetNodeOrNull<Node3D>("UsePosition")??host;
            Vector3 position=Vector3.Zero;for(Node3D at=marker;at!=null&&at!=root;at=at.GetParent() as Node3D)position=at.Transform*position;
            result.Add(new(){["id"]=id,["name"]=host.Name.ToString(),["to"]=portal.TargetLevel?.Id??"",["entry"]=portal.TargetEntry,
                ["position"]=position,["enabled"]=portal.IsActive,["automatic"]=portal.Automatic,["hours"]=portal.NpcTravelHours});
        }
        return result;
    }
    internal static void SynchronizeRoutes(WorldDefinition world)
    {
        if(!world.Levels.Any(l=>l.ExportPortalRoutes))return;
        var routes=new Godot.Collections.Array<WorldRouteDefinition>(world.Routes.Where(r=>r.SourcePortalId.Length==0||!world.Levels.Any(l=>l.Id==r.From.Id&&l.ExportPortalRoutes)));
        var catalog=new WorldCatalog(world);
        foreach(var level in world.Levels.Where(l=>l.ExportPortalRoutes))
        foreach(var portal in ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath).Portals)
        {
            string target=portal["to"].AsString(),entry=portal["entry"].AsString(),id=portal["id"].AsString();
            if(!portal["automatic"].AsBool()||target.Length==0||target==level.Id)continue;
            WorldCatalog.Require(catalog.Levels.TryGetValue(target,out var to),"传送门目标关卡未登记："+portal["name"]);
            WorldCatalog.Require(catalog.Entrances.TryGetValue((target,entry),out var arrival),"传送门目标入口不存在："+to.DisplayName+" / "+entry);
            // Existing authored roads own their direction, permission and duration; never add a bypass.
            if(world.Routes.Any(r=>r.SourcePortalId.Length==0&&(r.From.Id==level.Id&&r.To.Id==target&&r.ToEntry==entry
                ||r.Bidirectional&&r.To.Id==level.Id&&r.From.Id==target&&r.FromEntry==entry)))continue;
            var old=world.Routes.SingleOrDefault(r=>r.SourcePortalId==id&&r.From.Id==level.Id);
            Vector3 departure=portal["position"].AsVector3();double hours=portal["hours"].AsDouble();bool enabled=portal["enabled"].AsBool();
            if(old!=null&&old.To.Id==target&&old.ToEntry==entry&&old.FromEntry==level.DefaultEntry&&old.FromPosition==departure&&old.ToPosition==arrival
                &&old.TravelHours==hours&&old.Enabled==enabled&&!old.Bidirectional&&old.UseExplicitPositions&&old.DisplayName==portal["name"].AsString())
            {routes.Add(old);continue;}
            routes.Add(new(){Id=old?.Id??"route/"+level.Id+"/"+id,DisplayName=portal["name"].AsString(),SourcePortalId=id,
                From=level,To=to,FromEntry=level.DefaultEntry,ToEntry=entry,FromPosition=departure,ToPosition=arrival,UseExplicitPositions=true,
                Bidirectional=false,TravelHours=hours,Enabled=enabled,Public=old?.Public??true,Permission=old?.Permission});
        }
        // Validate all generated edges before publishing them.
        using var candidate=new WorldDefinition{Locations=world.Locations,Levels=world.Levels,ObjectTypes=world.ObjectTypes,Routes=routes};
        _=new WorldRoutes(new WorldCatalog(candidate));
        if(world.Routes.SequenceEqual(routes))return;
        world.Routes=routes;
        if(world.ResourcePath.Length>0)Save(world,world.ResourcePath);
    }
}
#endif
