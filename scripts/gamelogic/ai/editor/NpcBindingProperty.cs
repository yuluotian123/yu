#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[Tool]
public partial class NpcBindingProperty : EditorProperty
{
    sealed record Choice(string Id,string Label);
    readonly VBoxContainer _body=new();
    readonly Resource _owner;readonly string _property;
    string _stamp;
    public NpcBindingProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcBindingProperty(Resource owner,string property):this(){_owner=owner;_property=property;}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        bool input=_owner is NpcActionInputDefinition;
        var targets=NpcBindingAdapters.All.Select(a=>new Choice(input?"target:"+a.Id:a.Id,input?"目标 · "+a.Label:a.Label));
        Choice[] choices=(input?new[]{new Choice("number","数字"),new Choice("boolean","布尔"),new Choice("text","文本")}:Array.Empty<Choice>()).Concat(targets).ToArray();
        string value=_owner.Get(_property).ToString();string stamp=value+"|"+string.Join("|",choices.Select(c=>c.Id+":"+c.Label));
        if(stamp==_stamp)return;bool refresh=_stamp!=null;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label=input?"输入类型":"对象类型";
        var button=new Button{Text=choices.FirstOrDefault(c=>c.Id==value)?.Label??"类型已失效，请重新选择",Disabled=choices.Length==0,ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        button.Pressed+=()=>{var popup=new SearchablePopup<Choice>(choices,c=>c.Label,_=>Label,c=>c.Label);popup.OnItemSelected+=c=>EmitChanged(_property,c.Id);popup.ShowBelow(button);};_body.AddChild(button);
        if(_owner is NpcActionInputDefinition {IsTarget:true})
            _body.AddChild(new Label{Text="目标由选择器连线提供；将选择器输出连接到本输入端口。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
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
        string stamp=string.Join("|",items.Select(r=>r.GetInstanceId()+":"+((NpcActionInputDefinition)r).Name+":"+((NpcActionInputDefinition)r).InputType));
        if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        Label="行动输入";
        for(int i=0;i<items.Length;i++)
        {
            int index=i;var item=items[i];var row=new HBoxContainer();
            var input=(NpcActionInputDefinition)item;
            var edit=new Button{Text=input.Name+" · "+(input.IsTarget?"目标":"参数"),ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
            edit.Pressed+=()=>EmitSignal(SignalName.ResourceSelected,_property,item);row.AddChild(edit);
            var remove=new Button{Text="×"};remove.Pressed+=()=>Commit(Items().Where((_,n)=>n!=index).ToArray());row.AddChild(remove);_body.AddChild(row);
        }
        void Add(bool target)
        {
            string prefix=target?"目标":"参数",name=prefix;int n=1;while(Items().OfType<NpcActionInputDefinition>().Any(b=>b.Name==name))name=prefix+n++;
            var resource=new NpcActionInputDefinition{Name=name,InputType=target?"target:"+(NpcBindingAdapters.All.FirstOrDefault()?.Id??"point"):"number"};
            if(!target)resource.Value=new();
            Commit(Items().Append(resource).ToArray());
        }
        var addTarget=new Button{Text="+ 添加目标输入"};addTarget.Pressed+=()=>Add(true);_body.AddChild(addTarget);
        var addValue=new Button{Text="+ 添加普通参数"};addValue.Pressed+=()=>Add(false);_body.AddChild(addValue);
        _body.AddChild(new Label{Text="目标输入只接选择器连线；普通参数使用表达式或常量。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Commit(Resource[] resources)
    {
        Variant value=Variant.From(new Godot.Collections.Array<NpcActionInputDefinition>(resources.Cast<NpcActionInputDefinition>()));
        EmitChanged(_property,value);_stamp=null;Callable.From(_UpdateProperty).CallDeferred();
    }
}
#endif
