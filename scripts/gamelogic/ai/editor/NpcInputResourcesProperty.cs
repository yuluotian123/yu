#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

[Tool]
public partial class NpcInputResourcesProperty : EditorProperty
{
    readonly VBoxContainer _body=new();
    readonly NpcActionInputDefinition _owner;
    string _stamp;
    public NpcInputResourcesProperty(){AddChild(_body);SetBottomEditor(_body);}
    public NpcInputResourcesProperty(NpcActionInputDefinition owner):this(){_owner=owner;}
    static string CapabilityName(string capability)=>capability=="capacity"?"使用容量":capability;
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var capabilities=NpcBindingAdapters.Find(_owner.Adapter)?.Capabilities??Array.Empty<string>();
        Visible=_owner.ValueType==CharacterValueType.Symbol&&capabilities.Count>0;
        if(!Visible)return;
        Label="资源要求";
        string stamp=_owner.Adapter+"|"+string.Join("|",_owner.ResourceRequirements)+"|"+string.Join("|",capabilities);
        if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        foreach(string requirement in _owner.ResourceRequirements)
        {
            var row=new HBoxContainer();
            row.AddChild(new Label{Text=capabilities.Contains(requirement)?CapabilityName(requirement):"能力已失效："+requirement,AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill});
            var remove=new Button{Text="移除"};remove.Pressed+=()=>Commit(_owner.ResourceRequirements.Where(c=>c!=requirement).ToArray());
            row.AddChild(remove);_body.AddChild(row);
        }
        if(_owner.ResourceRequirements.Length==0)_body.AddChild(new Label{Text="仅引用对象，不占用资源。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
        var available=capabilities.Except(_owner.ResourceRequirements,StringComparer.Ordinal).ToArray();
        var add=new Button{Text="+ 添加资源要求",Disabled=available.Length==0};
        add.Pressed+=()=>
        {
            void Select(string capability)=>Commit(_owner.ResourceRequirements.Append(capability).Distinct(StringComparer.Ordinal).ToArray());
            if(available.Length==1){Select(available[0]);return;}
            var popup=new SearchablePopup<string>(available,CapabilityName,_=>"对象资源能力",CapabilityName);popup.OnItemSelected+=Select;popup.ShowBelow(add);
        };_body.AddChild(add);
        _body.AddChild(new Label{Text="仅在本行动执行期间占用；完成、失败、超时或取消时自动释放。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Commit(string[] requirements)
    {
        EmitChanged(nameof(NpcActionInputDefinition.ResourceRequirements),Variant.From(requirements));
        _stamp=null;Callable.From(_UpdateProperty).CallDeferred();
    }
}
#endif
