#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public static class NpcTargetCatalog
{
    public static NpcTargetSelectorDefinition[] Available(NpcGraphAsset graph)=>graph.Nodes.OfType<NpcRootNodeData>()
        .SelectMany(r=>graph.GetOutgoingConnections(r.Id,2)).Where(e=>e.IsAvailable)
        .Select(e=>graph.FindNodeById(e.ToNode)).OfType<NpcTargetSelectorNodeData>()
        .Select(n=>graph.Resolve(n.DefinitionId)).OfType<NpcTargetSelectorDefinition>().Where(s=>s.Enabled).DistinctBy(s=>s.DefinitionId).ToArray();
}

/// <summary>Name-based references and native Inspector undo; IDs are storage only.</summary>
[Tool]
public partial class NpcTargetProperty : EditorProperty
{
    sealed record Choice(string Id,string Name);
    readonly VBoxContainer _body=new();
    readonly NpcGraphAsset _graph;
    readonly Resource _owner;
    readonly string _property;
    string _stamp;
    public NpcTargetProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcTargetProperty(NpcGraphAsset graph,Resource owner,string property):this(){_graph=graph;_owner=owner;_property=property;}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        Choice[] choices=NpcExpressionEditorCatalog.Collect(_graph).Where(s=>s.Parameter&&s.Type==CharacterValueType.Symbol).Select(s=>new Choice(s.Key,s.Label)).ToArray();
        string value=_owner.Get(_property).ToString();
        string stamp=value+"\n"+string.Join("\n",choices.Select(c=>c.Id+":"+c.Name));if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=_property switch
        {
            nameof(NpcTargetSelectorDefinition.ParameterKey)=>"目标角色参数",
            nameof(NpcTargetSelectorDefinition.Source)=>"候选来源",
            nameof(NpcTargetSelectorDefinition.Selection)=>"选择策略",
            _=>_property
        };
        if(_property is nameof(NpcTargetSelectorDefinition.Source) or nameof(NpcTargetSelectorDefinition.Selection))
        {
            var select=new OptionButton();
            string[] labels=_property==nameof(NpcTargetSelectorDefinition.Source)
                ?new[]{"感知到的对象","世界中的对象","身份参数指定的对象","当前位置对应的对象","已知对象"}
                :new[]{"提供多个候选给 GOAP","最近一个","评分最低的一个","评分最高的一个"};
            foreach(string label in labels)select.AddItem(label);select.Select(_owner.Get(_property).AsInt32());
            select.ItemSelected+=i=>EmitChanged(_property,(int)i);_body.AddChild(select);return;
        }
        string chosen=choices.FirstOrDefault(c=>c.Id==value)?.Name;
        var button=new Button{Text=chosen??(value.Length==0?"选择文本角色参数":"引用不可用，请重新选择"),Disabled=choices.Length==0,
            ClipText=true,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,TooltipText=chosen??"选择已注册项；原引用会保留至重新选择。"};
        button.Pressed+=()=>
        {
            var popup=new SearchablePopup<Choice>(choices,c=>c.Name,_=>"身份参数",c=>c.Name);
            popup.OnItemSelected+=c=>{if(GodotObject.IsInstanceValid(this)&&!IsQueuedForDeletion())EmitChanged(_property,c.Id);};popup.ShowBelow(button);
        };
        _body.AddChild(button);

    }
}
#endif
