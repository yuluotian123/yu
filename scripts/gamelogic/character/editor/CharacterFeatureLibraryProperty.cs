#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic;

[Tool]
public partial class CharacterFeatureLibraryProperty : EditorProperty
{
    readonly VBoxContainer _body=new();
    readonly CharacterFeatureLibrary _owner;
    readonly LineEdit _search=new(){PlaceholderText="搜索特征名称或说明"};
    string _stamp;
    public CharacterFeatureLibraryProperty(){AddChild(_body);SetBottomEditor(_body);}
    public CharacterFeatureLibraryProperty(CharacterFeatureLibrary owner):this(){_owner=owner;Label="角色特征定义库";_search.TextChanged+=_=>{_stamp=null;_UpdateProperty();};}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        string stamp=_search.Text+"|"+string.Join("|",_owner.Definitions.Select(d=>d==null?"null":d.DefinitionId+":"+d.DisplayName+":"+d.Description+":"+d.Kind+":"+d.Enabled+":"+d.Minimum+":"+d.Maximum+":"+d.DefaultValue+":"+d.RecoveryPerHour));
        if(stamp==_stamp)return;_stamp=stamp;
        if(_search.GetParent()==_body)_body.RemoveChild(_search);
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        _body.AddChild(_search);
        foreach(var kind in Enum.GetValues<CharacterFeatureKind>())
        {
            var definitions=_owner.Definitions.Where(d=>d!=null&&d.Kind==kind&&(_search.Text.Length==0||(d.DisplayName+" "+d.Description).Contains(_search.Text,StringComparison.OrdinalIgnoreCase))).ToArray();
            _body.AddChild(new Label{Text=CharacterFeatureDefinition.KindName(kind)+" · "+definitions.Length});
            foreach(var definition in definitions)
            {
                var row=new HBoxContainer();
                var edit=new Button{Text=definition.DisplayName+(definition.Enabled?"":"（已停用）"),TooltipText=definition.Description,ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
                edit.Pressed+=()=>EmitSignal(SignalName.ResourceSelected,nameof(CharacterFeatureLibrary.Definitions),definition);row.AddChild(edit);
                var remove=new Button{Text="移除",TooltipText="引用此定义的角色需要一并调整；原引用会保留并报告失效。"};
                remove.Pressed+=()=>Commit(new(_owner.Definitions.Where(d=>d!=definition)));row.AddChild(remove);_body.AddChild(row);
            }
            var add=new Button{Text="+ 新建"+CharacterFeatureDefinition.KindName(kind)};
            add.Pressed+=()=>Commit(_owner.AddDefinition(kind));_body.AddChild(add);
        }
        foreach(var definition in _owner.Definitions.Where(d=>d==null))
        {
            var remove=new Button{Text="移除空定义"};remove.Pressed+=()=>Commit(new(_owner.Definitions.Where(d=>d!=null)));_body.AddChild(remove);break;
        }
        try{_owner.Compile();}catch(ArgumentException error){_body.AddChild(new Label{Text=error.Message,AutowrapMode=TextServer.AutowrapMode.WordSmart});}
        _body.AddChild(new Label{Text="在库中统一定义名称、范围与默认值；角色通过选择条目配置个体值。修改共享定义后保存定义库资源。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Commit(Godot.Collections.Array<CharacterFeatureDefinition> definitions)
    {EmitChanged(nameof(CharacterFeatureLibrary.Definitions),Variant.From(definitions));_stamp=null;Callable.From(_UpdateProperty).CallDeferred();}
}
#endif
