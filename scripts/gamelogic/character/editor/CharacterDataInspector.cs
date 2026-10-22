#if TOOLS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameLogic.AI;
using Godot;
namespace GameLogic;

[Tool]
public partial class CharacterDataInspector : EditorInspectorPlugin
{
    CharacterProfile _profile;
    public override bool _CanHandle(GodotObject target)=>target is CharacterWorldDefinition or CharacterDataComponent3D or CharacterProfile or CharacterNeedsDefinition or CharacterNeedDefinition or CharacterFeatureLibrary or CharacterFeatureDefinition or CharacterFeatureValue or CharacterPersonalityDefinition or CharacterAffectDefinition or CharacterRelationsDefinition;
    public override void _ParseBegin(GodotObject target)
    {
        if(target is CharacterDataComponent3D component)_profile=component.Profile;
        else if(target is CharacterProfile profile)_profile=profile;
    }
    public override bool _ParseProperty(GodotObject target,Variant.Type type,string name,PropertyHint hint,string hintString,PropertyUsageFlags usage,bool wide)
    {
        if(target is CharacterWorldDefinition world&&name is nameof(CharacterWorldDefinition.Objects) or nameof(CharacterWorldDefinition.KnownObjects))
        {AddPropertyEditor(name,new CharacterWorldObjectProperty(world,name==nameof(CharacterWorldDefinition.Objects)));return true;}
        if(target is CharacterFeatureLibrary library&&name==nameof(CharacterFeatureLibrary.Definitions))
        {AddPropertyEditor(name,new CharacterFeatureLibraryProperty(library));return true;}
        if(_profile==null||!Contains(_profile,target))
            _profile=Engine.IsEditorHint()?FindProfile(EditorInterface.Singleton.GetEditedSceneRoot(),target):null;
        if(target is CharacterFeatureValue value&&name is nameof(CharacterFeatureValue.UseDefaultRecovery) or nameof(CharacterFeatureValue.RecoveryPerHour)&&CharacterFeatureEditors.Kind(_profile,value)!=CharacterFeatureKind.Affect)return true;
        var featureEditor=CharacterFeatureEditors.Create(_profile,target,name);
        if(featureEditor!=null){AddPropertyEditor(name,featureEditor);return true;}
        if(target is CharacterNeedsDefinition needs&&name==nameof(CharacterNeedsDefinition.Needs))
        {AddPropertyEditor(name,new CharacterNeedsProperty(needs));return true;}
        if(target is not CharacterNeedDefinition||name!=nameof(CharacterNeedDefinition.RateMultiplier))return false;
        if(_profile==null)return false;
        var context=new NpcGraphAsset{CharacterContext=_profile,IncludeDecisionSymbols=false};
        AddPropertyEditor(name,new NpcExpressionProperty(context,target,name,CharacterValueType.Number));return true;
    }
    static CharacterProfile FindProfile(Node node,GodotObject target)
    {
        if(node==null)return null;
        if(node is GameObject3D actor)foreach(var component in actor.Components.OfType<CharacterDataComponent3D>())if(Contains(component.Profile,target))return component.Profile;
        foreach(Node child in node.GetChildren())if(FindProfile(child,target) is {} profile)return profile;
        return null;
    }
    static bool Contains(Resource root,GodotObject target)
    {
        var seen=new HashSet<Resource>();
        bool Visit(Resource current)
        {
            if(current==null||!seen.Add(current))return false;if(current==target)return true;
            foreach(var property in current.GetType().GetProperties().Where(p=>p.CanRead&&p.GetCustomAttribute<ExportAttribute>()!=null))
            {
                var value=property.GetValue(current);
                if(value is Resource child&&Visit(child)||value is IEnumerable items&&items.Cast<object>().OfType<Resource>().Any(Visit))return true;
            }
            return false;
        }
        return Visit(root);
    }
}
#endif
