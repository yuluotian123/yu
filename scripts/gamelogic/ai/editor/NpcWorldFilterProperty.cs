#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>World filters use configured names; stable type, capability and operation IDs are storage only.</summary>
[Tool]
public partial class NpcWorldFilterProperty : EditorProperty
{
    sealed record Choice(string Id,string Label,string Group,WorldObjectTypeDefinition Type=null);
    readonly VBoxContainer _body=new();
    readonly NpcGraphAsset _graph;
    readonly NpcTargetSelectorDefinition _selector;
    readonly string _property;
    string _stamp;
    public NpcWorldFilterProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcWorldFilterProperty(NpcGraphAsset graph,NpcTargetSelectorDefinition selector,string property):this(){_graph=graph;_selector=selector;_property=property;}
    static string CapabilityName(string key)=>key switch
    {"interaction"=>"交互","stock"=>"库存","opening_hours"=>"开放时段","usage_capacity"=>"使用容量","liquid"=>"液体",_=>key};
    Choice[] Choices()
    {
        var types=new List<WorldObjectTypeDefinition>();var entries=new List<WorldCatalogEntry>();
        try
        {
            var catalog=WorldAuthoringCompiler.EditorCatalog;
            types.AddRange(catalog.Definition.ObjectTypes);entries.AddRange(catalog.Objects.Values);
        }
        catch(ArgumentException){/* The world plugin reports invalid or missing generated catalogs. */}
        Node root=Engine.IsEditorHint()?EditorInterface.Singleton.GetEditedSceneRoot():null;
        root??=GodotObject.IsInstanceValid(_graph.EditorHostContext)?_graph.EditorHostContext:null;
        void Visit(Node node)
        {
            if(node is GameObject3D host)foreach(var binding in host.Components.OfType<WorldObjectComponent3D>())
                if(binding.ObjectType!=null)
                {types.Add(binding.ObjectType);entries.Add(new("",binding.DisplayName,binding.ObjectType,null,binding.Location,binding.ExtraTags,binding.OwnerId,Array.AsReadOnly(binding.Definitions.ToArray())));}
            foreach(var child in node.GetChildren())Visit(child);
        }
        if(GodotObject.IsInstanceValid(root))Visit(root);
        if(_selector.ObjectType!=null)types.Add(_selector.ObjectType);
        types=types.Where(t=>t.SupportsAdapter(_selector.Adapter)).DistinctBy(t=>t.Id).ToList();
        if(_property==nameof(NpcTargetSelectorDefinition.ObjectType))
            return types.Select(t=>new Choice(t.Id,t.DisplayName,"世界对象类型",t)).OrderBy(c=>c.Label,StringComparer.Ordinal).ToArray();
        if(_selector.ObjectType!=null)
        {types=types.Where(t=>t.Id==_selector.ObjectType.Id).ToList();entries=entries.Where(e=>e.Type.Id==_selector.ObjectType.Id).ToList();}
        var configurations=types.Select(t=>(Type:t,Capabilities:(IEnumerable<WorldCapabilityDefinition>)t.Capabilities))
            .Concat(entries.Where(e=>e.Type.SupportsAdapter(_selector.Adapter)).Select(e=>(Type:e.Type,Capabilities:e.EffectiveCapabilities)));
        return configurations.SelectMany(c=>_property==nameof(NpcTargetSelectorDefinition.RequiredCapabilities)
                ?c.Capabilities.Select(cap=>new Choice(cap.Key,CapabilityName(cap.Key),c.Type.DisplayName))
                :c.Capabilities.OfType<WorldInteractionCapability>().SelectMany(cap=>cap.Operations).Select(op=>new Choice(op.Id,op.DisplayName,c.Type.DisplayName)))
            .DistinctBy(c=>c.Id).OrderBy(c=>c.Label,StringComparer.Ordinal).ToArray();
    }
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_selector))return;
        var choices=Choices();bool type=_property==nameof(NpcTargetSelectorDefinition.ObjectType);
        var selected=type?Array.Empty<string>():_selector.Get(_property).AsStringArray();
        string stamp=(_selector.ObjectType?.Id??"")+"|"+string.Join("|",selected)+"|"+string.Join("|",choices.Select(c=>c.Id+":"+c.Label+":"+c.Group));
        if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=type?"世界对象类型":_property==nameof(NpcTargetSelectorDefinition.RequiredCapabilities)?"必需对象能力":"必需交互操作";
        if(type)
        {
            var row=new HBoxContainer();
            var button=new Button{Text=_selector.ObjectType?.DisplayName??"不限对象类型",SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,Disabled=choices.Length==0};
            button.Pressed+=()=>Show(button,choices,c=>Commit(c.Type));row.AddChild(button);
            var clear=new Button{Text="清除",Disabled=_selector.ObjectType==null};clear.Pressed+=()=>Commit(default(Variant));row.AddChild(clear);_body.AddChild(row);return;
        }
        foreach(string key in selected)
        {
            var row=new HBoxContainer();var choice=choices.FirstOrDefault(c=>c.Id==key);
            row.AddChild(new Label{Text=choice?.Label??"未找到配置："+key,TooltipText=key,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});
            var remove=new Button{Text="移除"};remove.Pressed+=()=>Commit(Variant.From(selected.Where(k=>k!=key).ToArray()));row.AddChild(remove);_body.AddChild(row);
        }
        if(selected.Length==0)_body.AddChild(new Label{Text="不限制。"});
        var available=choices.Where(c=>!selected.Contains(c.Id)).ToArray();
        var add=new Button{Text="+ 添加要求",Disabled=available.Length==0};
        add.Pressed+=()=>Show(add,available,c=>Commit(Variant.From(selected.Append(c.Id).ToArray())));_body.AddChild(add);
        _body.AddChild(new Label{Text="对象必须提供全部所选项；仅筛选支持范围，不申请资源或执行操作。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Show(Button button,Choice[] choices,Action<Choice> select)
    {
        var popup=new SearchablePopup<Choice>(choices,c=>c.Label,c=>c.Group,c=>c.Label+" · "+c.Id);
        popup.OnItemSelected+=c=>{if(GodotObject.IsInstanceValid(this)&&!IsQueuedForDeletion())select(c);};popup.ShowBelow(button);
    }
    void Commit(Variant value){EmitChanged(_property,value);_stamp=null;Callable.From(_UpdateProperty).CallDeferred();}
}
#endif
