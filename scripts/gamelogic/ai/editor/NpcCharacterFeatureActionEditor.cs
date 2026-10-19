#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic.AI;

public static class NpcCharacterFeatureActionEditor
{
    public static Control Build(NpcAdjustAffectAction action,GraphEditorContext context)
    {
        var root=new VBoxContainer();
        Feature(root,context,CharacterFeatureKind.Affect,()=>action.Dimension,id=>action.Dimension=id);
        Amount(root,context,action.Amount,value=>action.Amount=value);return root;
    }
    public static Control Build(NpcAdjustRelationAction action,GraphEditorContext context)
    {
        var root=new VBoxContainer();var profile=Profile(context);
        var parameters=profile?.Identity?.Parameters.Where(p=>p!=null&&p.ValueType==CharacterValueType.Symbol).ToArray()??Array.Empty<CharacterScalarDefinition>();
        root.AddChild(new Label{Text="联系人参数"});
        var contact=new Button{Text=parameters.Any(p=>p.Key==action.ContactParameter)?action.ContactParameter:"选择角色的联系人参数",Disabled=parameters.Length==0,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        contact.Pressed+=()=>
        {
            var popup=new SearchablePopup<CharacterScalarDefinition>(parameters,p=>p.Key,_=>"角色文本参数",p=>p.Key);
            popup.OnItemSelected+=p=>{action.ContactParameter=p.Key;contact.Text=p.Key;context?.CurrentGraph?.MarkDirty();};popup.ShowBelow(contact);
        };root.AddChild(contact);
        Feature(root,context,CharacterFeatureKind.Relation,()=>action.Dimension,id=>action.Dimension=id);
        Amount(root,context,action.Amount,value=>action.Amount=value);return root;
    }
    static CharacterProfile Profile(GraphEditorContext context)=>(context?.RootGraph as NpcGraphAsset??context?.CurrentGraph as NpcGraphAsset)?.CharacterContext;
    static void Feature(VBoxContainer root,GraphEditorContext context,CharacterFeatureKind kind,Func<string> selected,Action<string> set)
    {
        var profile=Profile(context);var definitions=CharacterFeatureEditors.Definitions(profile,kind);
        bool enabled=kind==CharacterFeatureKind.Affect?profile?.Affect?.Enabled==true:profile?.Relations?.Enabled==true;
        if(!enabled)definitions=Array.Empty<CharacterFeatureDefinition>();
        root.AddChild(new Label{Text=CharacterFeatureDefinition.KindName(kind)+"维度"});
        var definition=definitions.FirstOrDefault(d=>d.DefinitionId==selected());
        var button=new Button{Text=definition!=null?CharacterFeatureEditors.Name(definition):selected().Length>0?"原定义引用不可用，请重新选择":"选择共享定义",Disabled=definitions.Length==0,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        button.Pressed+=()=>
        {
            var popup=new SearchablePopup<CharacterFeatureDefinition>(definitions,CharacterFeatureEditors.Name,d=>d.Category,d=>d.DisplayName+" "+d.Description);
            popup.OnItemSelected+=d=>{set(d.DefinitionId);button.Text=CharacterFeatureEditors.Name(d);context?.CurrentGraph?.MarkDirty();};popup.ShowBelow(button);
        };root.AddChild(button);
        if(definitions.Length==0)root.AddChild(new Label{Text="请从角色的 NPC 图打开，并在 Profile 中绑定定义库、启用相应模块。",AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    static void Amount(VBoxContainer root,GraphEditorContext context,double value,Action<double> set)
    {
        root.AddChild(new Label{Text="变化量"});
        var number=new SpinBox{MinValue=-1000000,MaxValue=1000000,Step=.01,Value=value};
        number.ValueChanged+=next=>{set(next);context?.CurrentGraph?.MarkDirty();};root.AddChild(number);
    }
}
#endif
