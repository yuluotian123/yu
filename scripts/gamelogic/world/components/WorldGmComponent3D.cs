using System;
using System.Collections.Generic;
using System.Linq;
using Framework;
using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldGmComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.VFX;
    Control _page;
    Label _status;
    RichTextLabel _records;
    OptionButton _levels;
    LineEdit _entry;
    Button _switch;
    double _elapsed;
    readonly List<(GodotObject Source,StringName Signal,Callable Callback)> _bindings=new();
    WorldModule World=>(WorldModule)ModuleSystem.GetModule<IWorldModule>();
    T Find<T>(string name) where T:Node=>Owner.GetNodeOrNull<T>("%"+name);
    public override void OnInit()
    {
        _page=Find<Control>("WorldPage");_status=Find<Label>("WorldStatus");_records=Find<RichTextLabel>("WorldRecords");
        _levels=Find<OptionButton>("WorldLevels");_entry=Find<LineEdit>("WorldEntry");_switch=Find<Button>("WorldSwitch");
        var tabs=Find<TabContainer>("Tabs");tabs.SetTabTitle(_page.GetIndex(),"世界与关卡");
        foreach(var level in World.Catalog.Levels.Values){_levels.AddItem(level.DisplayName);_levels.SetItemMetadata(_levels.ItemCount-1,level.Id);}
        Bind(_switch,Button.SignalName.Pressed,Callable.From(()=>Switch()));
        Bind(Find<Button>("WorldCancel"),Button.SignalName.Pressed,Callable.From(()=>World.CancelTransition()));
        Bind(_page,CanvasItem.SignalName.VisibilityChanged,Callable.From(Refresh));Refresh();
    }
    async void Switch()
    {
        if(_levels.Selected<0)return;
        // Unload destroys this GM; restore its input locks before the transaction checkpoints input state.
        Owner.GetComponent<RuntimeGmComponent3D>()?.ShowPanel(false);
        try{if(!await World.SwitchAsync(_levels.GetItemMetadata(_levels.Selected).AsString(),_entry.Text))GD.PushWarning(World.LastError);}
        catch(Exception error){GD.PushError(error.Message);}
    }
    void Bind(GodotObject source,StringName signal,Callable callback){source.Connect(signal,callback);_bindings.Add((source,signal,callback));}
    public override void OnUpdate(double delta){if(_page?.IsVisibleInTree()!=true)return;_elapsed+=delta;if(_elapsed<.25)return;_elapsed=0;Refresh();}
    void Refresh()
    {
        var world=World;
        _status.Text=$"当前关卡：{world.CurrentLevelId} · {world.Status} · {world.Progress:P0}\n上次切换 {world.LastTransitionMilliseconds:0.0} ms · 路径 {world.PathQueriesLastFrame}/{world.PathQueriesPerFrame} · {world.LastError}";
        _switch.Disabled=!world.IsOpen||world.IsSwitching;
        if(world.Population is {} population)_status.Text+=$"\n世界角色 {population.Actors.Count} · 详细 {population.Actors.Values.Count(a=>a.Mode=="Detailed")} / 旅行 {population.Actors.Values.Count(a=>a.Mode=="Transit")} / 离屏 {population.Actors.Values.Count(a=>a.Mode=="Distant")} · 事件 {population.LastEvents}/{population.EventsPerFrame} · {population.LastMilliseconds:0.00} ms · 积压 {population.Backlog} · {population.LastError}";
        _records.Text=$"目录共 {world.Catalog.Objects.Count} 个对象，最多展示前 256 个。\n"+string.Join("\n",world.Catalog.Objects.Values.Take(256).Select(o=>$"{o.DisplayName} · {o.Id} · {o.Level.DisplayName} / {o.Location?.DisplayName??o.Level.Location?.DisplayName} · {world.ResolveStatus(o.Id)} · 能力 {string.Join(", ",o.EffectiveCapabilities.Select(c=>c.Key))} · 库存 {(world.ExistingState(o.Id)?.Capability<WorldStockState>("stock")?.Amount??o.Capability<WorldStockCapability>()?.InitialAmount)?.ToString("0.##")??"—"}"))
            +"\n\nNPC 最近查询\n"+string.Join("\n",world.EnvironmentFor(Owner)?.Actors.Values.Select(a=>a.ActorId+"\n"+a.QueryDiagnostics)??Array.Empty<string>());
        if(world.Population!=null)_records.Text+="\n\n世界角色名册\n"+string.Join("\n",world.Population.Actors.Values.Take(128).Select(a=>$"{a.Definition.Id} · {a.Mode} · {a.Level} · 活动 {a.Agent?.CurrentActivity} · 下一事件 {a.NextHours:0.000} h · 路段 {a.Travel?.Leg} / 进度 {a.Travel?.ElapsedHours:0.000} h"));
    }
    public override void OnDestroy(){foreach(var b in _bindings)if(GodotObject.IsInstanceValid(b.Source)&&b.Source.IsConnected(b.Signal,b.Callback))b.Source.Disconnect(b.Signal,b.Callback);_bindings.Clear();}
}
