using System;
using System.Linq;
using Framework;
using GameLogic.AI;
using Godot;
namespace GameLogic;

/// <summary>Player target selection and presentation; shared WorldInteraction owns execution rules.</summary>
[Tool, GlobalClass]
public partial class PlayerWorldInteractionComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.Interaction-1;
    [Export] public string InputAction {get;set;}="player_interact";
    [Export] public NodePath PanelPath {get;set;}=new("WorldInteractionHUD/Root/Panel");
    [Export] public NodePath HintPath {get;set;}=new("WorldInteractionHUD/Root/Panel/Content/Hint");
    [Export] public NodePath MessagePath {get;set;}=new("WorldInteractionHUD/Root/Panel/Content/Message");
    [Export] public NodePath ChoicesPath {get;set;}=new("WorldInteractionHUD/Root/Panel/Content/Choices");
    WorldModule _world;
    IInputModule _input;
    PlayerCharacterInputComponent3D _playerInput;
    Control _panel;
    Label _hint,_message;
    VBoxContainer _choices;
    WorldInteractionComponent3D _point;
    double _refreshAt,_feedbackTime;
    bool _menu;
    public string CurrentPointId=>GodotObject.IsInstanceValid(_point)?_point.PointId:"";
    public string PromptText=>_hint?.Text??"";
    public WorldInteractionResult LastResult {get;private set;}
    bool Enabled=>IsActive&&!_world.IsSwitching&&_input.IsLayerEnabled("Combat")&&_input.IsLayerEnabled("UI")&&_playerInput is {IsActive:true,GameplayBlocked:false};
    public override void OnInit()
    {
        _world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();_input=ModuleSystem.GetModule<IInputModule>();
        _playerInput=Owner.GetComponent<PlayerCharacterInputComponent3D>();
        WorldCatalog.Require(_playerInput!=null&&InputMap.HasAction(InputAction),"玩家交互缺少输入组件或 InputMap 操作。");
        _panel=Owner.GetNode<Control>(PanelPath);_hint=Owner.GetNode<Label>(HintPath);_message=Owner.GetNode<Label>(MessagePath);_choices=Owner.GetNode<VBoxContainer>(ChoicesPath);
        _panel.Hide();_choices.Hide();
    }
    public override void OnUpdate(double delta)
    {
        if(!Enabled){ClearTarget();_feedbackTime=0;_message.Text="";_panel.Hide();return;}
        _feedbackTime=Math.Max(0,_feedbackTime-delta);if(_feedbackTime==0)_message.Text="";
        _refreshAt-=delta;if(_refreshAt<=0){_refreshAt=.1;RefreshTarget();}
        var focus=Owner.GetViewport().GuiGetFocusOwner();
        bool ownsFocus=focus==null||_menu&&_choices.IsAncestorOf(focus);
        if(ownsFocus&&_input.IsJustPressed(InputAction,"Combat")&&_input.TryConsumeJustPressed(InputAction,"Combat"))
        {
            if(_menu&&focus is Button selected){if(selected.HasMeta("operation"))Interact(selected.GetMeta("operation").AsString());else CloseMenu();}
            else Activate();
        }
        _panel.Visible=_point!=null||_feedbackTime>0;
    }
    public void RefreshTarget()
    {
        if(!Enabled){ClearTarget();return;}
        var environment=AiWorldComponent3D.Find(Owner);
        var next=environment?.Nearby(Owner.GlobalPosition,0)
            .Where(p=>p.IsActive&&p.Operations?.Count>0&&p.ContainsPosition(Owner.GlobalPosition))
            .OrderBy(p=>Owner.GlobalPosition.DistanceSquaredTo(p.UsePosition)).ThenBy(p=>p.PointId,StringComparer.Ordinal)
            .Take(16).FirstOrDefault(p=>WorldInteraction.HasLineOfSight(Owner,p));
        if(next!=_point){CloseMenu();_point=next;}
        if(_point==null){_hint.Text="";return;}
        var operation=_point.Operations[0];var check=WorldInteraction.Check(Owner,_point,operation.Id);
        _hint.Text=$"[ E ] {_point.DisplayName} · {(_point.Operations.Count==1?operation.DisplayName:"选择操作")}"+(check.Success?"":"\n"+check.Message);
    }
    public bool Activate()
    {
        RefreshTarget();if(_point==null)return false;
        if(_point.Operations.Count==1)return Interact(_point.Operations[0].Id);
        OpenMenu();return true;
    }
    public bool Interact(string operationId)
    {
        return ExecuteAt(_point,operationId);
    }
    bool ExecuteAt(WorldInteractionComponent3D target,string operationId)
    {
        if(!Enabled||!GodotObject.IsInstanceValid(target))return false;
        LastResult=WorldInteraction.Execute(Owner,target,operationId);_message.Text=LastResult.Message;_feedbackTime=2.5;CloseMenu();RefreshTarget();return LastResult.Success;
    }
    void OpenMenu()
    {
        if(_menu)return;_menu=true;
        foreach(var operation in _point.Operations)
        {
            string id=operation.Id;var check=WorldInteraction.Check(Owner,_point,id);
            var button=new Button{Text=operation.DisplayName+(check.Success?"":" · "+check.Message),Disabled=!check.Success};button.SetMeta("operation",id);
            var target=_point;button.Pressed+=()=>ExecuteAt(target,id);_choices.AddChild(button);
        }
        var close=new Button{Text="关闭"};close.Pressed+=CloseMenu;_choices.AddChild(close);_choices.Show();
        _choices.GetChildren().OfType<Button>().FirstOrDefault(b=>!b.Disabled)?.GrabFocus();
    }
    void CloseMenu()
    {
        _menu=false;if(!GodotObject.IsInstanceValid(_choices))return;
        var viewport=Owner.IsInsideTree()?Owner.GetViewport():null;
        var focus=viewport?.GuiGetFocusOwner();
        if(focus!=null&&_choices.IsAncestorOf(focus))viewport.GuiReleaseFocus();
        foreach(var child in _choices.GetChildren()){_choices.RemoveChild(child);child.QueueFree();}_choices.Hide();
    }
    void ClearTarget(){CloseMenu();_point=null;if(_hint!=null)_hint.Text="";}
    public override void OnDestroy(){ClearTarget();_panel?.Hide();_world=null;_input=null;_playerInput=null;}
}
