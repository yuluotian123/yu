#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public static class NpcTargetCatalog
{
    public sealed record Point(string Id,string Name,string Path);
    public static IReadOnlyList<Point> Points(Node root)
    {
        var points=new List<Point>();
        IReadOnlyDictionary<GameObject3D,string> identities=null;
        if(root is GameObject3D level&&level.Components.OfType<WorldLevelContextComponent3D>().Any())
            try{identities=WorldIdentity.Resolve(root);}catch(ArgumentException){return points;}
        void Visit(Node node)
        {
            if(node is GameObject3D host)foreach(var point in host.Components.OfType<WorldInteractionComponent3D>())
            {
                var binding=host.Components.OfType<WorldObjectComponent3D>().SingleOrDefault();if(binding==null)continue;
                string id=identities?.GetValueOrDefault(host)??host.PersistentId;
                string label=binding.DisplayName;
                if(id.Length>0)points.Add(new(id,label.Length>0?label:host.Name.ToString(),root.GetPathTo(host).ToString()));
            }
            foreach(Node child in node.GetChildren())Visit(child);
        }
        if(GodotObject.IsInstanceValid(root))Visit(root);
        // Ambiguous identities cannot be resolved safely at runtime.
        return points.GroupBy(p=>p.Id,StringComparer.Ordinal).Where(g=>g.Count()==1).Select(g=>g.Single()).OrderBy(p=>p.Name,StringComparer.Ordinal).ThenBy(p=>p.Path,StringComparer.Ordinal).ToArray();
    }
    public static IReadOnlyList<Point> Points(NpcGraphAsset graph)
    {
        Node root=Engine.IsEditorHint()?EditorInterface.Singleton.GetEditedSceneRoot():null;
        root??=GodotObject.IsInstanceValid(graph.EditorHostContext)?graph.EditorHostContext.Owner??graph.EditorHostContext:null;
        var local=Points(root);
        IEnumerable<Point> directory=Array.Empty<Point>();
        try{directory=WorldAuthoringCompiler.EditorCatalog.Objects.Values.Where(p=>p.Type.SupportsAdapter("point")).Select(p=>new Point(p.Id,p.DisplayName,p.Level.DisplayName+" / "+p.Location?.DisplayName)).ToArray();}
        catch(ArgumentException){/* Missing catalogs are reported by the authoring plugin. */}
        return directory.Concat(local).GroupBy(p=>p.Id,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray();
    }
}

/// <summary>Name-based references and native Inspector undo; IDs are storage only.</summary>
[Tool]
public partial class NpcTargetProperty : EditorProperty
{
    sealed record Choice(string Id,string Name,string Group);
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
        bool fixedObject=_property==nameof(NpcTargetSelectorDefinition.FixedObjectId);
        bool objectBinding=_property==nameof(NpcTargetSelectorDefinition.ParameterKey)&&(_owner as NpcTargetSelectorDefinition)?.Source==NpcTargetSource.CharacterBinding;
        bool placeBinding=_property==nameof(NpcTargetSelectorDefinition.LocationBinding);
        Choice[] choices=objectBinding
            ?_graph.CharacterContext?.World?.Objects.Select(p=>new Choice(p.Key,p.Key+" · "+p.Value,"角色对象绑定")).ToArray()??Array.Empty<Choice>()
            :placeBinding?_graph.CharacterContext?.World?.Locations.Select(p=>new Choice(p.Key,p.Key+" · "+p.Value?.DisplayName,"角色地点绑定")).ToArray()??Array.Empty<Choice>()
            :fixedObject
            ?((_owner as NpcTargetSelectorDefinition)?.Adapter=="point"?NpcTargetCatalog.Points(_graph).Select(p=>new Choice(p.Id,p.Name,p.Path)).ToArray():Array.Empty<Choice>())
            :NpcExpressionEditorCatalog.Collect(_graph).Where(s=>s.Parameter&&s.Type==CharacterValueType.Symbol).Select(s=>new Choice(s.Key,s.Label,"角色参数")).ToArray();
        string value=_owner.Get(_property).ToString();
        string stamp=value+"\n"+string.Join("\n",choices.Select(c=>c.Id+":"+c.Name+":"+c.Group));if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=_property switch
        {
            nameof(NpcTargetSelectorDefinition.ParameterKey)=>objectBinding?"角色对象绑定":"目标角色参数",
            nameof(NpcTargetSelectorDefinition.FixedObjectId)=>"固定对象",
            nameof(NpcTargetSelectorDefinition.Source)=>"候选来源",
            nameof(NpcTargetSelectorDefinition.Selection)=>"选择策略",
            nameof(NpcTargetSelectorDefinition.Scope)=>"地点范围",
            nameof(NpcTargetSelectorDefinition.Information)=>"信息权限",
            nameof(NpcTargetSelectorDefinition.LocationBinding)=>"角色地点绑定",
            _=>_property
        };
        if(_property is nameof(NpcTargetSelectorDefinition.Source) or nameof(NpcTargetSelectorDefinition.Selection) or nameof(NpcTargetSelectorDefinition.Scope) or nameof(NpcTargetSelectorDefinition.Information))
        {
            var select=new OptionButton();
            string[] labels=_property switch
            {
                nameof(NpcTargetSelectorDefinition.Source)=>new[]{"感知到的对象","本关卡已加载的对象（授权状态）","角色参数指定的对象","当前位置对应的对象","已知对象","固定世界对象","世界目录查询","角色对象绑定"},
                nameof(NpcTargetSelectorDefinition.Scope)=>new[]{"当前关卡","当前地点","指定地点","角色地点绑定","整个世界","当前活动地点"},
                nameof(NpcTargetSelectorDefinition.Information)=>new[]{"已知具体对象","已知地点的公开服务","授权访问权威状态"},
                _=>new[]{"提供多个候选给 GOAP","最近一个","评分最低的一个","评分最高的一个"}
            };
            foreach(string label in labels)select.AddItem(label);select.Select(_owner.Get(_property).AsInt32());
            select.ItemSelected+=i=>EmitChanged(_property,(int)i);_body.AddChild(select);return;
        }
        string chosen=choices.FirstOrDefault(c=>c.Id==value)?.Name;
        var button=new Button{Text=chosen??(value.Length==0?(fixedObject?"选择场景交互点":"选择文本角色参数"):"引用不可用，请重新选择"),Disabled=choices.Length==0,
            ClipText=true,TextOverrunBehavior=TextServer.OverrunBehavior.TrimEllipsis,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,TooltipText=chosen??"选择已注册项；原引用会保留至重新选择。"};
        button.Pressed+=()=>
        {
            var popup=new SearchablePopup<Choice>(choices,c=>c.Name,c=>c.Group,c=>c.Name+" · "+c.Group);
            popup.OnItemSelected+=c=>{if(GodotObject.IsInstanceValid(this)&&!IsQueuedForDeletion())EmitChanged(_property,c.Id);};popup.ShowBelow(button);
        };
        _body.AddChild(button);
        if(fixedObject&&choices.Length==0)_body.AddChild(new Label{Text="当前场景没有可选择的交互点。请配置交互点或宿主的稳定标识；重复标识的对象不能选择。",AutowrapMode=TextServer.AutowrapMode.WordSmart});

    }
}
#endif
