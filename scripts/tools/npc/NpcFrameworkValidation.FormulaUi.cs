using GameLogic;
#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using GameLogic.AI;
using Godot;

public partial class NpcFrameworkValidation
{
    static IEnumerable<T> FormulaControls<T>(Node node) where T:Node=>node.FindChildren("*",typeof(T).Name,true,false).OfType<T>();
    void ValidateFormulaUi(NpcGraphAsset source)
    {
        var graph=source.CloneForCharacter();var goal=graph.Definitions.OfType<NpcGoalDefinition>().First();
        CharacterExpressionDefinition Number(double n)=>new(){Number=n};
        CharacterExpressionDefinition Fact(string key)=>new(){Kind=CharacterExpressionKind.Fact,Key=key};
        CharacterExpressionDefinition Op(CharacterExpressionKind kind,params CharacterExpressionDefinition[] inputs)=>NpcExpressionEditor.Operation(kind,inputs);
        var symbols=NpcExpressionEditorCatalog.For(graph,goal);
        var original=Op(CharacterExpressionKind.All,Op(CharacterExpressionKind.AtLeast,Fact("needs.hunger.value"),Number(.65)),Op(CharacterExpressionKind.AtLeast,Fact("world.hour"),Number(7)));
        var nested=Op(CharacterExpressionKind.All,original.Inputs[0],Op(CharacterExpressionKind.Any,Op(CharacterExpressionKind.Greater,Fact("world.rain"),Number(.1)),original.Inputs[1]));
        string formatted=NpcExpressionPresentation.Format(nested,symbols);
        Check(formatted.Contains("饥饿")&&formatted.Contains("≥")&&formatted.Contains("且")&&formatted.Contains("或")&&formatted.Contains('(')&&!formatted.Contains("needs.hunger"),"Readable formula preview lost names, operators or nested grouping");
        Check(NpcExpressionPresentation.Format(Number(1e-10),symbols)!="0","Small nonzero constants are displayed as zero");
        Check(NpcExpressionPresentation.Validate(nested,CharacterValueType.Boolean,symbols)=="","Valid nested condition was rejected locally");
        Check(NpcExpressionPresentation.Validate(Op(CharacterExpressionKind.Add,Fact("self.location"),Number(1)),CharacterValueType.Number,symbols).Length>0,"Invalid numeric operand type was accepted");
        Check(NpcExpressionPresentation.Validate(Fact("missing"),CharacterValueType.Number,symbols).Contains("未注册"),"Removed symbols are not reported locally");
        Check(NpcExpressionPresentation.Validate(Op(CharacterExpressionKind.Divide,Number(1),Number(0)),CharacterValueType.Number,symbols).Length>0,"Constant zero denominator is not reported");
        var cycle=Op(CharacterExpressionKind.All);cycle.Inputs.Add(cycle);
        Check(NpcExpressionPresentation.Format(cycle,symbols).Contains("循环")&&NpcExpressionPresentation.Validate(cycle,CharacterValueType.Boolean,symbols).Length>0,"Cyclic formulas hang the preview or validation");cycle.Inputs.Clear();
        goal.Activation=original;
        var property=(NpcExpressionProperty)graph.CreateCustomPropertyEditor(goal,nameof(goal.Activation));AddChild(property);property._UpdateProperty();
        Check(!FormulaControls<SpinBox>(property).Any()&&FormulaControls<Button>(property).Any(b=>b.Text=="编辑公式…"),"Inspector still embeds the entire formula tree");
        Check(property.GetCombinedMinimumSize().X<=280,"Compact formula preview forces a wide Inspector");
        using(var history=new UndoRedo())
        {
            int commits=0;
            property.PropertyChanged+=(name,value,_,_)=>{commits++;history.CreateAction("Apply formula draft");history.AddDoProperty(goal,name,value);history.AddUndoProperty(goal,name,goal.Get(name));history.CommitAction();};
            var dialog=property.CreateEditor();var numbers=FormulaControls<SpinBox>(dialog).ToArray();
            Check(numbers.Length==2&&dialog.MinSize.X>=700&&!dialog.Unresizable,"Large formula editor is missing editable rows or resizing");
            numbers[0].Value=.9;numbers[1].Value=9;
            Check(commits==0&&ReferenceEquals(goal.Activation,original)&&original.Inputs[0].Inputs[1].Number==.65,"Draft edits changed the source before Apply");
            Check(Math.Abs(dialog.Draft.Inputs[0].Inputs[1].Number-.9)<1e-8&&Math.Abs(dialog.Draft.Inputs[1].Inputs[1].Number-9)<1e-8,"Draft edits reverted another row");
            FormulaControls<Button>(dialog).Single(b=>b.Text=="撤销编辑").EmitSignal(Button.SignalName.Pressed);
            Check(Math.Abs(dialog.Draft.Inputs[0].Inputs[1].Number-.9)<1e-8&&Math.Abs(dialog.Draft.Inputs[1].Inputs[1].Number-7)<1e-8,"Draft undo does not preserve the earlier row edit");
            FormulaControls<Button>(dialog).Single(b=>b.Text=="重做编辑").EmitSignal(Button.SignalName.Pressed);
            dialog.ApplyDraft();Check(commits==1&&Math.Abs(goal.Activation.Inputs[1].Inputs[1].Number-9)<1e-8,"Apply is not one native Inspector transaction");
            history.Undo();Check(goal.Activation==original,"Applied formula cannot undo to the original resource");history.Redo();property._UpdateProperty();
            var cancelled=property.CreateEditor();FormulaControls<SpinBox>(cancelled).First().Value=.3;
            FormulaControls<Button>(cancelled).Single(b=>b.Text=="取消").EmitSignal(Button.SignalName.Pressed);
            Check(commits==1&&Math.Abs(goal.Activation.Inputs[0].Inputs[1].Number-.9)<1e-8,"Cancelling a draft committed changes");
            var stale=property.CreateEditor();var external=Op(CharacterExpressionKind.AtMost,Fact("needs.hunger.value"),Number(.2));goal.Activation=external;
            stale.ApplyDraft();Check(commits==1&&goal.Activation==external,"Stale dialog overwrote an externally changed expression");stale.QueueFree();
        }
        property.QueueFree();
        bool invalidCommitted=false;
        var invalidDialog=new NpcExpressionDialog(graph,goal,CharacterValueType.Number,Fact("removed"),"Invalid",_=>invalidCommitted=true);AddChild(invalidDialog);
        invalidDialog.ApplyDraft();Check(!invalidCommitted&&FormulaControls<Button>(invalidDialog).Single(b=>b.Text=="应用公式").Disabled,"Invalid formula can be applied");invalidDialog.QueueFree();
        var requiredProperty=(NpcExpressionProperty)graph.CreateCustomPropertyEditor(goal,nameof(goal.Completion));AddChild(requiredProperty);requiredProperty._UpdateProperty();
        var requiredDialog=requiredProperty.CreateEditor();var oldCompletion=goal.Completion;
        FormulaControls<Button>(requiredDialog).Single(b=>b.Text=="清空公式").EmitSignal(Button.SignalName.Pressed);requiredDialog.ApplyDraft();
        Check(requiredDialog.Draft==null&&goal.Completion==oldCompletion&&FormulaControls<Button>(requiredDialog).Single(b=>b.Text=="应用公式").Disabled,"Required completion formula can be cleared and applied");requiredProperty.QueueFree();
        var product=Op(CharacterExpressionKind.Multiply,Fact("needs.hunger.value"),Number(1));
        var palette=new NpcExpressionDialog(graph,goal,CharacterValueType.Number,product,"Palette",_=>{});AddChild(palette);
        FormulaControls<Button>(palette).First(b=>b.Text.Contains("饥饿")).EmitSignal(Button.SignalName.Pressed);
        var tree=FormulaControls<Tree>(palette).Single();
        IEnumerable<TreeItem> Items(TreeItem parent)
        {for(var child=parent?.GetFirstChild();child!=null;child=child.GetNext()){yield return child;foreach(var nestedItem in Items(child))yield return nestedItem;}}
        Check(!Items(tree.GetRoot()).Any(i=>i.GetTooltipText(0).Contains("Boolean")),"Parameter library ignores the selected operand's numeric type");
        var search=FormulaControls<LineEdit>(palette).Single(l=>l.PlaceholderText.Contains("搜索"));search.Text="降雨";search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);
        var rain=Items(tree.GetRoot()).Single(i=>i.GetTooltipText(0).StartsWith("world.rain",StringComparison.Ordinal));rain.Select(0);
        Check(palette.Draft.Inputs[0].Key=="world.rain"&&product.Inputs[0].Key=="needs.hunger.value","Searchable parameter library did not replace only the selected operand");palette.QueueFree();
        CharacterExpressionDefinition edited=null;
        var many=Op(CharacterExpressionKind.Add,Number(10),Number(2),Number(3));
        var math=NpcExpressionEditor.Build(graph,goal,CharacterValueType.Number,many,(e,_)=>edited=e);AddChild(math);
        Check(!FormulaControls<Button>(math).Any(b=>b.Text=="计算"),"Numeric formula still has a redundant calculation wrapper");
        var operation=FormulaControls<OptionButton>(math).First();operation.EmitSignal(OptionButton.SignalName.ItemSelected,1L);
        Check(edited.Freeze().Evaluate(_=>default).Number==5&&many.Inputs.Count==3,"Switching to subtraction discarded extra operands");math.QueueFree();
        var sum=Op(CharacterExpressionKind.Add,Number(2),Number(-3));
        var clamp=NpcExpressionEditor.Build(graph,goal,CharacterValueType.Number,sum,(e,_)=>edited=e);AddChild(clamp);
        FormulaControls<OptionButton>(clamp).First().EmitSignal(OptionButton.SignalName.ItemSelected,6L);
        Check(edited.Inputs.Single().Kind==CharacterExpressionKind.Add&&edited.Freeze().Evaluate(_=>default).Number==0,"Clamping a formula discarded its existing calculation");clamp.QueueFree();
        ValidateDefinitionEntry(source);
    }
    void ValidateDefinitionEntry(NpcGraphAsset source)
    {
        var graph=source.CloneForCharacter();var goal=graph.Definitions.OfType<NpcGoalDefinition>().First();
        var need=graph.CharacterContext.Needs.Needs.First();
        Check(graph.GetResourceEditorTitle(need)==need.DisplayName&&!graph.GetResourceEditorTitle(need).Contains("Definition"),"Nested resource entry shows a code type instead of its configured name");
        Check(graph.GetResourceEditorDescription(goal).Contains("评分")&&graph.GetResourceEditorDescription(graph.Definitions.OfType<NpcActionDefinition>().First()).Contains("输入"),"Definition entry does not describe its configuration purpose");
        var editor=graph.CreateCustomPropertyEditor(graph,nameof(graph.Definitions));AddChild(editor);editor._UpdateProperty();
        Check(editor is NpcDefinitionsProperty,"Root NPC graph still uses the raw definition resource array");
        var search=FormulaControls<LineEdit>(editor).Single();var parent=search.GetParent();search.Text=goal.DisplayName;search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);
        Check(search.GetParent()==parent&&FormulaControls<Button>(editor).Any(b=>b.Text==goal.DisplayName),"Searching definitions lost the search control or configured names");
        var goalRow=FormulaControls<Button>(editor).Single(b=>b.Text==goal.DisplayName).GetParent();
        Check(goalRow.GetChildren().OfType<Button>().Single(b=>b.Text=="×").Disabled,"Definition library can remove a definition still used by a graph node");
        int originalCount=graph.Definitions.Count,commits=0;var extra=new NpcMemoryDefinition{DisplayName="未连接的示例定义"};
        using(var history=new UndoRedo())
        {
            editor.PropertyChanged+=(property,value,_,_)=>{commits++;history.CreateAction("Edit named definition library");history.AddDoProperty(graph,property,value);history.AddUndoProperty(graph,property,graph.Get(property));history.CommitAction();};
            ((NpcDefinitionsProperty)editor).AddExisting(extra);Check(graph.Definitions.Count==originalCount+1&&source.Definitions.Count==originalCount,"Adding a library definition changed the source graph or failed");
            history.Undo();Check(graph.Definitions.Count==originalCount,"Definition addition cannot be undone");history.Redo();
            ((NpcDefinitionsProperty)editor).AddExisting(extra);Check(commits==1,"Duplicate definition was added twice");
            search.Text=extra.DisplayName;search.EmitSignal(LineEdit.SignalName.TextChanged,search.Text);
            var row=FormulaControls<Button>(editor).Single(b=>b.Text==extra.DisplayName).GetParent();
            row.GetChildren().OfType<Button>().Single(b=>b.Text=="×").EmitSignal(Button.SignalName.Pressed);
            Check(!graph.Definitions.Contains(extra),"Unused definition cannot be removed");history.Undo();Check(graph.Definitions.Contains(extra),"Removing an unused definition cannot be undone");
        }
        editor.QueueFree();
    }
}
#endif
