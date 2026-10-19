using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>Serialized by the Flow graph; names are selected from the enclosing action.</summary>
public sealed class NpcInputReference
#if TOOLS
    : IGraphParameterEditor
#endif
{
    public string Name {get;set;}="地点";
#if TOOLS
    public Control CreateEditUI(GraphEditorContext context)
    {
        var root=new VBoxContainer();
        var npc=context.RootGraph as NpcGraphAsset;
        var action=npc?.Definitions.OfType<NpcActionDefinition>().FirstOrDefault(a=>a.ExecutionGraph==context.CurrentGraph||context.ParentGraphs.Contains(a.ExecutionGraph));
        var button=new Button{Text=action?.Inputs.Any(i=>i.Name==Name)==true?Name:Name.Length==0?"无输入（使用自身）":"输入已失效："+Name,ClipText=true};
        button.Pressed+=()=>
        {
            var choices=new[]{""}.Concat(action?.Inputs.Select(i=>i.Name)??System.Array.Empty<string>()).ToArray();
            var popup=new SearchablePopup<string>(choices,n=>n.Length==0?"无输入（使用自身）":n,_=>"行动输入",n=>n);
            popup.OnItemSelected+=n=>{Name=n;button.Text=n.Length==0?"无输入（使用自身）":n;context.CurrentGraph.MarkDirty();};popup.ShowBelow(button);
        };
        root.AddChild(button);return root;
    }
#endif
}
