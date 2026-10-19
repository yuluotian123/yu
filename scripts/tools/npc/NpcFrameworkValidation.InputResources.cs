using System;
using System.Linq;
using GameLogic.AI;
using Godot;

public partial class NpcFrameworkValidation
{
    void ValidateInputResourceRequirements(NpcGraphAsset source)
    {
        var graph=(NpcGraphAsset)source.Duplicate(true);NpcProgram.Compile(graph);
        var action=graph.Definitions.OfType<NpcActionDefinition>().First(a=>a.Inputs.Count>0);
        var input=(NpcActionInputDefinition)action.Inputs[0];
        Check(action.Resources.Count==0&&input.ResourceRequirements.SequenceEqual(new[]{"capacity"}),"Existing action resources were not moved into their input");
        bool Editable(Resource resource,string property)=>resource.GetPropertyList().Any(p=>p["name"].AsString()==property&&((PropertyUsageFlags)p["usage"].AsInt64()).HasFlag(PropertyUsageFlags.Editor));
        Check(!Editable(action,nameof(action.Resources)),"The old action-level resource list is still visible");
        Check(Editable(input,nameof(input.ResourceRequirements)),"Object input does not expose resource requirements");
        var variable=new NpcBindingDefinition{Name="床",Adapter="point"};
        Check(!variable.GetPropertyList().Any(p=>p["name"].AsString()==nameof(input.ResourceRequirements)),"Plan variables contain action-specific resource settings");
        var frozen=NpcProgram.Compile(graph).Goals.SelectMany(g=>g.Actions).First(a=>a.Id==action.DefinitionId);
        input.ResourceRequirements=Array.Empty<string>();
        Check(frozen.Resources.Single().Input==input.Name&&NpcProgram.Compile(graph).Goals.SelectMany(g=>g.Actions).First(a=>a.Id==action.DefinitionId).Resources.Count==0,"Input requirements were not frozen or could not be cleared");
        action.Resources.Add(new(){Input=input.Name,Capability="capacity"});action.Resources.Add(new(){Input=input.Name,Capability="capacity"});
        Check(graph.MigrateActionBindings()&&action.Resources.Count==0&&input.ResourceRequirements.SequenceEqual(new[]{"capacity"}),"Legacy claims were lost or duplicated during migration");
        Check(!graph.MigrateActionBindings(),"Moving resource requirements is not idempotent");
        var duplicate=(NpcActionInputDefinition)input.DuplicateDeep(Resource.DeepDuplicateMode.All);duplicate.ResourceRequirements=Array.Empty<string>();
        Check(input.ResourceRequirements.Length==1,"Copied input changed the original requirements");
        input.ValueType=NpcValueType.Number;Check(!Editable(input,nameof(input.ResourceRequirements)),"Numeric input exposes resource requirements");
        input.ValueType=NpcValueType.Boolean;Check(!Editable(input,nameof(input.ResourceRequirements)),"Boolean input exposes resource requirements");
        input.ValueType=NpcValueType.Symbol;input.Adapter="";Check(!Editable(input,nameof(input.ResourceRequirements)),"Plain text input exposes resource requirements");input.Adapter="point";
#if TOOLS
        Check(graph.CreateCustomPropertyEditor(action,nameof(action.Resources))==null,"The legacy top-level resource editor is still registered");
        var editor=graph.CreateCustomPropertyEditor(input,nameof(input.ResourceRequirements));AddChild(editor);editor._UpdateProperty();
        Check(editor is NpcInputResourcesProperty&&!editor.FindChildren("*",nameof(LineEdit),true,false).Any(),"Requirements require a hand-entered input or capability ID");
        using(var history=new UndoRedo())
        {
            editor.PropertyChanged+=(property,value,_,_)=>{history.CreateAction("Edit input resource requirements");history.AddDoProperty(input,property,value);history.AddUndoProperty(input,property,input.Get(property));history.CommitAction();};
            editor.FindChildren("*",nameof(Button),true,false).OfType<Button>().Single(b=>b.Text=="移除").EmitSignal(Button.SignalName.Pressed);
            Check(input.ResourceRequirements.Length==0,"Requirement removal was not applied");history.Undo();Check(input.ResourceRequirements.SequenceEqual(new[]{"capacity"}),"Requirement removal cannot be undone");
            history.Redo();editor._UpdateProperty();
            editor.FindChildren("*",nameof(Button),true,false).OfType<Button>().Single(b=>b.Text=="+ 添加资源要求").EmitSignal(Button.SignalName.Pressed);
            Check(input.ResourceRequirements.SequenceEqual(new[]{"capacity"}),"Registered requirement could not be added by name");
            history.Undo();Check(input.ResourceRequirements.Length==0,"Adding a requirement cannot be undone");history.Redo();
        }
        editor.QueueFree();
#endif
        input.ResourceRequirements=new[]{"capacity","removed_capability"};bool rejected=false;
        try{NpcProgram.Compile(graph);}catch(ArgumentException){rejected=true;}
        Check(rejected&&input.ResourceRequirements.Contains("removed_capability"),"Invalid requirement was accepted or silently discarded");input.ResourceRequirements=new[]{"capacity"};
        graph.SaveJsonFields();string path="res://.godot/npc/input_resources/roundtrip.tres";
        Check(ResourceSaver.Save(graph,path)==Error.Ok,"Could not save input resource requirements");
        var loaded=ResourceLoader.Load<NpcGraphAsset>(path,cacheMode:ResourceLoader.CacheMode.Ignore);
        var restored=(NpcActionDefinition)loaded.Resolve(action.DefinitionId);
        Check(restored.Resources.Count==0&&restored.Inputs[0] is NpcActionInputDefinition saved&&saved.ResourceRequirements.SequenceEqual(new[]{"capacity"}),"Input requirements were lost or moved back to the action after reload");
        Check(NpcProgram.Compile(loaded).Goals.SelectMany(g=>g.Actions).First(a=>a.Id==action.DefinitionId).Resources.Single().Input==input.Name,"Reloaded input requirements do not drive runtime resource leases");
    }
}
