#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic;

public static class CharacterFeatureEditors
{
    public static CharacterFeatureDefinition[] Definitions(CharacterProfile profile,CharacterFeatureKind kind)=>profile?.ResolveFeatureLibrary()?.Definitions.Where(d=>d is {Enabled:true}&&d.Kind==kind).ToArray()??Array.Empty<CharacterFeatureDefinition>();
    public static CharacterFeatureKind? Kind(CharacterProfile profile,CharacterFeatureValue value)=>
        profile?.Personality?.Dimensions.Contains(value)==true?CharacterFeatureKind.Personality:
        profile?.Personality?.Preferences.Contains(value)==true?CharacterFeatureKind.Preference:
        profile?.Affect?.Dimensions.Contains(value)==true?CharacterFeatureKind.Affect:
        profile?.Relations?.Dimensions.Contains(value)==true?CharacterFeatureKind.Relation:null;
    public static CharacterFeatureValue[] Entries(CharacterProfile profile,CharacterFeatureKind kind)=>kind switch
    {CharacterFeatureKind.Personality=>profile?.Personality?.Dimensions.ToArray()??Array.Empty<CharacterFeatureValue>(),CharacterFeatureKind.Preference=>profile?.Personality?.Preferences.ToArray()??Array.Empty<CharacterFeatureValue>(),CharacterFeatureKind.Affect=>profile?.Affect?.Dimensions.ToArray()??Array.Empty<CharacterFeatureValue>(),CharacterFeatureKind.Relation=>profile?.Relations?.Dimensions.ToArray()??Array.Empty<CharacterFeatureValue>(),_=>Array.Empty<CharacterFeatureValue>()};
    public static EditorProperty Create(CharacterProfile profile,GodotObject owner,string property)
    {
        if(owner is CharacterPersonalityDefinition&&property is nameof(CharacterPersonalityDefinition.Dimensions) or nameof(CharacterPersonalityDefinition.Preferences) or nameof(CharacterPersonalityDefinition.Traits))
            return new CharacterFeatureValuesProperty(profile,(Resource)owner,property,property==nameof(CharacterPersonalityDefinition.Dimensions)?CharacterFeatureKind.Personality:property==nameof(CharacterPersonalityDefinition.Preferences)?CharacterFeatureKind.Preference:CharacterFeatureKind.Trait);
        if(owner is CharacterAffectDefinition&&property==nameof(CharacterAffectDefinition.Dimensions))return new CharacterFeatureValuesProperty(profile,(Resource)owner,property,CharacterFeatureKind.Affect);
        if(owner is CharacterRelationsDefinition&&property==nameof(CharacterRelationsDefinition.Dimensions))return new CharacterFeatureValuesProperty(profile,(Resource)owner,property,CharacterFeatureKind.Relation);
        if(owner is CharacterFeatureValue value)
        {
            if(property==nameof(CharacterFeatureValue.DefinitionId))return new CharacterFeatureReferenceProperty(profile,value);
            if(property==nameof(CharacterFeatureValue.UseDefaultValue)||property==nameof(CharacterFeatureValue.UseDefaultRecovery))return new CharacterFeatureToggleProperty(value,property);
            if(property==nameof(CharacterFeatureValue.Number)||property==nameof(CharacterFeatureValue.RecoveryPerHour))return new CharacterFeatureNumberProperty(profile,value,property);
        }
        return null;
    }
    public static string Name(CharacterFeatureDefinition definition)=>string.IsNullOrWhiteSpace(definition.DisplayName)?"未命名"+CharacterFeatureDefinition.KindName(definition.Kind):definition.DisplayName;
}

[Tool]
public partial class CharacterFeatureValuesProperty : EditorProperty
{
    readonly VBoxContainer _body=new();
    readonly CharacterProfile _profile;readonly Resource _owner;readonly string _property;readonly CharacterFeatureKind _kind;
    string _stamp;
    public CharacterFeatureValuesProperty(){AddChild(_body);SetBottomEditor(_body);}
    public CharacterFeatureValuesProperty(CharacterProfile profile,Resource owner,string property,CharacterFeatureKind kind):this()
    {_profile=profile;_owner=owner;_property=property;_kind=kind;Label=CharacterFeatureDefinition.KindName(kind)+(_kind==CharacterFeatureKind.Trait?"选择":"个体覆盖");}
    CharacterFeatureValue[] Values()=>_owner.Get(_property).AsGodotArray().Select(v=>v.AsGodotObject() as CharacterFeatureValue).ToArray();
    string[] Ids()=>_kind==CharacterFeatureKind.Trait?((CharacterPersonalityDefinition)_owner).Traits:Values().Select(v=>v?.DefinitionId??"").ToArray();
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var ids=Ids();var definitions=CharacterFeatureEditors.Definitions(_profile,_kind);
        string stamp=string.Join("|",ids)+"/"+string.Join("|",definitions.Select(d=>d.DefinitionId+":"+d.DisplayName+":"+d.DefaultValue))+(_kind==CharacterFeatureKind.Trait?"":"/"+string.Join("|",Values().Select(v=>v==null?"null":v.GetInstanceId()+":"+v.UseDefaultValue+":"+v.Number)));
        if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        for(int i=0;i<ids.Length;i++)
        {
            int index=i;string id=ids[i];var definition=definitions.FirstOrDefault(d=>d.DefinitionId==id);
            var row=new HBoxContainer();
            string text=definition==null?"引用已失效："+id:CharacterFeatureEditors.Name(definition);
            if(_kind==CharacterFeatureKind.Trait)row.AddChild(new Label{Text=text,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart});
            else
            {
                var value=Values()[i];text+=value==null?"":value.UseDefaultValue?$" · 默认 {definition?.DefaultValue:G}":$" · {value.Number:G}";
                var edit=new Button{Text=text,Disabled=value==null,TooltipText=definition?.Description??"重新选择定义或移除失效项。",ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
                edit.Pressed+=()=>EmitSignal(SignalName.ResourceSelected,_property,value);row.AddChild(edit);
            }
            var remove=new Button{Text="移除"};remove.Pressed+=()=>
            {if(_kind==CharacterFeatureKind.Trait)Commit(Variant.From(Ids().Where((_,n)=>n!=index).ToArray()));else Commit(Variant.From(new Godot.Collections.Array<CharacterFeatureValue>(Values().Where((_,n)=>n!=index))));};
            row.AddChild(remove);_body.AddChild(row);
        }
        var available=definitions.Where(d=>!ids.Contains(d.DefinitionId,StringComparer.Ordinal)).ToArray();
        var add=new Button{Text="+ 添加"+CharacterFeatureDefinition.KindName(_kind),Disabled=available.Length==0};
        add.Pressed+=()=>
        {
            void Select(CharacterFeatureDefinition definition)
            {
                if(Ids().Contains(definition.DefinitionId,StringComparer.Ordinal))return;
                if(_kind==CharacterFeatureKind.Trait)Commit(Variant.From(Ids().Append(definition.DefinitionId).ToArray()));
                else Commit(Variant.From(new Godot.Collections.Array<CharacterFeatureValue>(Values().Append(new(){DefinitionId=definition.DefinitionId}))));
            }
            if(available.Length==1){Select(available[0]);return;}
            var popup=new SearchablePopup<CharacterFeatureDefinition>(available,CharacterFeatureEditors.Name,d=>d.Category,d=>d.DisplayName+" "+d.Description);
            popup.OnItemSelected+=Select;popup.ShowBelow(add);
        };_body.AddChild(add);
        _body.AddChild(new Label{Text=_profile==null?"请从角色 Profile 展开以选择特征。":_kind==CharacterFeatureKind.Trait?"从定义库选择特质；未选择的特质为否。":"未覆盖的维度采用定义库默认值；点击条目配置个体值。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    void Commit(Variant value){EmitChanged(_property,value);_stamp=null;Callable.From(_UpdateProperty).CallDeferred();}
}

[Tool]
public partial class CharacterFeatureReferenceProperty : EditorProperty
{
    readonly VBoxContainer _body=new();readonly CharacterProfile _profile;readonly CharacterFeatureValue _owner;string _stamp;
    public CharacterFeatureReferenceProperty(){AddChild(_body);SetBottomEditor(_body);}
    public CharacterFeatureReferenceProperty(CharacterProfile profile,CharacterFeatureValue owner):this(){_profile=profile;_owner=owner;Label="共享定义";}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var kind=CharacterFeatureEditors.Kind(_profile,_owner);
        var used=kind.HasValue?CharacterFeatureEditors.Entries(_profile,kind.Value).Where(v=>v!=null&&v!=_owner).Select(v=>v.DefinitionId).ToHashSet(StringComparer.Ordinal):new System.Collections.Generic.HashSet<string>();
        var definitions=kind.HasValue?CharacterFeatureEditors.Definitions(_profile,kind.Value).Where(d=>!used.Contains(d.DefinitionId)).ToArray():Array.Empty<CharacterFeatureDefinition>();
        string stamp=_owner.DefinitionId+"/"+string.Join("|",definitions.Select(d=>d.DefinitionId+":"+d.DisplayName));if(stamp==_stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        var selected=definitions.FirstOrDefault(d=>d.DefinitionId==_owner.DefinitionId);
        var button=new Button{Text=selected==null?"选择定义库中的条目":CharacterFeatureEditors.Name(selected),Disabled=definitions.Length==0,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        button.Pressed+=()=>{var popup=new SearchablePopup<CharacterFeatureDefinition>(definitions,CharacterFeatureEditors.Name,d=>d.Category,d=>d.DisplayName+" "+d.Description);popup.OnItemSelected+=d=>EmitChanged(nameof(CharacterFeatureValue.DefinitionId),d.DefinitionId);popup.ShowBelow(button);};_body.AddChild(button);
        if(selected!=null)_body.AddChild(new Label{Text=selected.Description,AutowrapMode=TextServer.AutowrapMode.WordSmart});
        else _body.AddChild(new Label{Text="原引用已保留；请从角色 Profile 进入并选择有效定义。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
}

[Tool]
public partial class CharacterFeatureToggleProperty : EditorProperty
{
    readonly CheckBox _toggle=new();readonly CharacterFeatureValue _owner;readonly string _property;
    public CharacterFeatureToggleProperty(){AddChild(_toggle);}
    public CharacterFeatureToggleProperty(CharacterFeatureValue owner,string property):this()
    {_owner=owner;_property=property;Label=property==nameof(CharacterFeatureValue.UseDefaultValue)?"使用定义默认值":"使用定义恢复速度";_toggle.Toggled+=value=>EmitChanged(_property,value);}
    public override void _UpdateProperty(){if(GodotObject.IsInstanceValid(_owner))_toggle.SetPressedNoSignal(_owner.Get(_property).AsBool());}
}

[Tool]
public partial class CharacterFeatureNumberProperty : EditorProperty
{
    readonly SpinBox _number=new(){Step=.01,AllowGreater=false,AllowLesser=false};
    readonly CharacterProfile _profile;readonly CharacterFeatureValue _owner;readonly string _property;
    public CharacterFeatureNumberProperty(){AddChild(_number);}
    public CharacterFeatureNumberProperty(CharacterProfile profile,CharacterFeatureValue owner,string property):this()
    {_profile=profile;_owner=owner;_property=property;Label=property==nameof(CharacterFeatureValue.Number)?"个体值或基线":"每游戏小时恢复";_number.ValueChanged+=value=>EmitChanged(_property,value);}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        var definition=_profile?.ResolveFeatureLibrary()?.Find(_owner.DefinitionId);bool recovery=_property==nameof(CharacterFeatureValue.RecoveryPerHour);
        _number.SetBlockSignals(true);_number.MinValue=recovery?0:definition?.Minimum??-1000000;_number.MaxValue=recovery?1000000:definition?.Maximum??1000000;
        _number.AllowGreater=recovery;_number.Value=_owner.Get(_property).AsDouble();_number.SetBlockSignals(false);
    }
}
#endif
