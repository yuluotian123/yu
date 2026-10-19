#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[Tool]
public partial class NpcNeedPolicyProperty : EditorProperty
{
    readonly NpcGraphAsset _graph;
    readonly NpcNeedPolicyDefinition _owner;
    readonly VBoxContainer _body=new();
    string _stamp;
    public NpcNeedPolicyProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcNeedPolicyProperty(NpcGraphAsset graph,NpcNeedPolicyDefinition owner):this(){_graph=graph;_owner=owner;Label="角色需求";}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var choices=_graph.CharacterContext?.Needs?.Needs.Where(n=>n!=null).ToArray()??Array.Empty<CharacterNeedDefinition>();
        string stamp=_owner.NeedId+"|"+string.Join("|",choices.Select(n=>n.NeedId+":"+n.DisplayName));if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        string LabelOf(CharacterNeedDefinition need)=>string.IsNullOrWhiteSpace(need.DisplayName)?need.NeedId:need.DisplayName;
        var chosen=choices.FirstOrDefault(n=>n.NeedId==_owner.NeedId);
        var button=new Button{Text=chosen!=null?LabelOf(chosen):_owner.NeedId.Length==0?"选择角色已有需求":"需求引用已失效："+_owner.NeedId,Disabled=choices.Length==0,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        button.Pressed+=()=>
        {
            var popup=new SearchablePopup<CharacterNeedDefinition>(choices,LabelOf,_=>"角色需求",n=>LabelOf(n)+" ["+n.NeedId+"]");
            popup.OnItemSelected+=n=>EmitChanged(nameof(NpcNeedPolicyDefinition.NeedId),n.NeedId);popup.ShowBelow(button);
        };
        _body.AddChild(button);
        if(choices.Length==0)_body.AddChild(new Label{Text="请先为场景宿主配置通用角色需求。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
}
#endif
