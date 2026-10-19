#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public static class NpcBindingCatalog
{
    public static NpcActionDefinition Action(NpcGraphAsset graph,GodotObject resource)=>graph.Definitions.OfType<NpcActionDefinition>()
        .FirstOrDefault(a=>a==resource||a.Inputs.Contains(resource as NpcBindingDefinition));
    public static bool IsVariable(NpcGraphAsset graph,NpcBindingDefinition binding)=>graph.Definitions.OfType<NpcPlanTemplateDefinition>().Any(p=>p.Variables.Contains(binding));
    public static NpcBindingDefinition[] Variables(NpcGraphAsset graph,NpcBindingDefinition binding)
    {
        var action=Action(graph,binding);if(action==null)return Array.Empty<NpcBindingDefinition>();
        var plans=graph.Definitions.OfType<NpcPlanTemplateDefinition>().Where(p=>p.Enabled&&p.ActionIds.Contains(action.DefinitionId)).ToArray();
        return plans.Length==0?Array.Empty<NpcBindingDefinition>():plans[0].Variables.Where(v=>v!=null&&v.ValueType==binding.ValueType&&v.Adapter==binding.Adapter&&
            plans.All(p=>p.Variables.Any(other=>other.Name==v.Name&&other.ValueType==v.ValueType&&other.Adapter==v.Adapter))).ToArray();
    }
}

[Tool]
public partial class NpcBindingProperty : EditorProperty
{
    sealed record Choice(string Id,string Label);
    readonly VBoxContainer _body=new();
    readonly NpcGraphAsset _graph;readonly Resource _owner;readonly string _property;
    string _stamp;
    public NpcBindingProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcBindingProperty(NpcGraphAsset graph,Resource owner,string property):this(){_graph=graph;_owner=owner;_property=property;}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var binding=_owner as NpcBindingDefinition;
        Choice[] choices=_property switch
        {
            nameof(NpcBindingDefinition.SelectorId)=>NpcTargetCatalog.Available(_graph).Select(s=>new Choice(s.DefinitionId,s.DisplayName)).ToArray(),
            nameof(NpcBindingDefinition.Variable)=>NpcBindingCatalog.Variables(_graph,binding).Select(v=>new Choice(v.Name,v.Name)).ToArray(),
            nameof(NpcBindingDefinition.Adapter)=>(_owner is NpcTargetSelectorDefinition?Array.Empty<Choice>():new[]{new Choice("","普通文本")}).Concat(NpcBindingAdapters.All.Select(a=>new Choice(a.Id,a.Label))).ToArray(),
            _=>Array.Empty<Choice>()
        };
        string value=_owner.Get(_property).ToString();string stamp=value+"|"+string.Join("|",choices.Select(c=>c.Id+":"+c.Label));
        if(stamp==_stamp)return;bool refresh=_stamp!=null;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=_property switch
        {
            nameof(NpcBindingDefinition.ValueType)=>"数据类型",nameof(NpcBindingDefinition.Source)=>"值的来源",nameof(NpcBindingDefinition.Adapter)=>"对象类型",
            nameof(NpcBindingDefinition.SelectorId)=>"候选选择器",nameof(NpcBindingDefinition.Variable)=>"计划变量",_=>_property
        };
        if(_property is nameof(NpcBindingDefinition.Source) or nameof(NpcBindingDefinition.ValueType))
        {
            var option=new OptionButton();
            var items=_property==nameof(NpcBindingDefinition.Source)?new[]{"表达式或常量","选择器候选","本次计划变量"}:new[]{"未知","布尔","数字","文本或对象引用"};
            foreach(string text in items)option.AddItem(text);
            if(_property==nameof(NpcBindingDefinition.ValueType))option.SetItemDisabled(0,true);
            else if(NpcBindingCatalog.IsVariable(_graph,binding))option.SetItemDisabled(2,true);
            option.Select(_owner.Get(_property).AsInt32());option.ItemSelected+=i=>EmitChanged(_property,(int)i);_body.AddChild(option);
        }
        else
        {
            var button=new Button{Text=choices.FirstOrDefault(c=>c.Id==value)?.Label??(value.Length==0?"选择已注册项":"引用已失效："+value),Disabled=choices.Length==0,ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            button.Pressed+=()=>{var popup=new SearchablePopup<Choice>(choices,c=>c.Label,_=>Label,c=>c.Label);popup.OnItemSelected+=c=>EmitChanged(_property,c.Id);popup.ShowBelow(button);};_body.AddChild(button);
            if(choices.Length==0)_body.AddChild(new Label{Text="暂无同类型的可选项；请先配置计划变量、选择器或对象输入。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        }
        if(refresh)Callable.From(()=>{if(GodotObject.IsInstanceValid(_owner))_owner.NotifyPropertyListChanged();}).CallDeferred();
    }
}

[Tool]
public partial class NpcBindingsProperty : EditorProperty
{
    readonly VBoxContainer _body=new();readonly Resource _owner;readonly string _property;string _stamp;
    public NpcBindingsProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcBindingsProperty(Resource owner,string property):this(){_owner=owner;_property=property;}
    Resource[] Items()=>_owner.Get(_property).AsGodotArray().Select(v=>v.AsGodotObject()).OfType<Resource>().ToArray();
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;var items=Items();
        string stamp=string.Join("|",items.Select(r=>r.GetInstanceId()+":"+(r as NpcBindingDefinition)?.Name));
        if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=_owner is NpcPlanTemplateDefinition?"计划变量":"行动输入";
        for(int i=0;i<items.Length;i++)
        {
            int index=i;var item=items[i];var row=new HBoxContainer();
            var edit=new Button{Text=((NpcBindingDefinition)item).Name,ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            edit.Pressed+=()=>EmitSignal(SignalName.ResourceSelected,_property,item);row.AddChild(edit);
            var remove=new Button{Text="×"};remove.Pressed+=()=>Commit(Items().Where((_,n)=>n!=index).ToArray());row.AddChild(remove);_body.AddChild(row);
        }
        var add=new Button{Text="+ 添加"+Label};add.Pressed+=()=>
        {
            string name="参数";int n=1;while(Items().OfType<NpcBindingDefinition>().Any(b=>b.Name==name))name="参数"+n++;
            NpcBindingDefinition resource=_owner is NpcActionDefinition?new NpcActionInputDefinition():new NpcBindingDefinition();
            resource.Name=name;resource.Value=new(){ValueType=CharacterValueType.Symbol};
            Commit(Items().Append(resource).ToArray());
        };_body.AddChild(add);
        _body.AddChild(new Label{Text="点击条目编辑；参数引用从当前已注册项中选择。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Commit(Resource[] resources)
    {
        Variant value=Variant.From(new Godot.Collections.Array<NpcBindingDefinition>(resources.Cast<NpcBindingDefinition>()));
        EmitChanged(_property,value);_stamp=null;Callable.From(_UpdateProperty).CallDeferred();
    }
}
#endif
