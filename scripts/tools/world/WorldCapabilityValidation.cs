using System;
using System.Linq;
using Framework;
using GameLogic;
using GameLogic.AI;
using GameLogic.Goap;
using Godot;

public partial class WorldCapabilityValidation : Node
{
    int _checks;
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    void Reject(Action action,string message){try{action();}catch(ArgumentException){_checks++;return;}throw new InvalidOperationException(message);}
    public override void _Ready()
    {
        var root=new GameObject3D{Name="Capabilities",Components=new(){new AiWorldComponent3D()}};
        try
        {
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/world-validation/capability-saves","res://.godot/world-validation/no-legacy"));
            var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();AddChild(root);
            var bottleType=new WorldObjectTypeDefinition{Capabilities=new(){new WorldLiquidCapability{VolumeLimit=2,InitialVolume=1}}};
            var bottle=new GameObject3D{Name="Bottle",PersistentId="cap_bottle",Components=new(WorldTestObjects.Components(bottleType))};root.AddChild(bottle);
            var binding=bottle.GetComponent<WorldObjectComponent3D>();var liquid=bottle.GetComponent<WorldLiquidComponent3D>();
            Check(world.ResolveObject("cap_bottle")==binding&&world.ResolvePoint("cap_bottle")==null,"A bottle must register without any fixed interaction point");
            Check(bottle.GetComponent<WorldStockComponent3D>()==null&&bottle.GetComponent<WorldOpeningHoursComponent3D>()==null&&bottle.GetComponent<WorldUsageCapacityComponent3D>()==null,"Bottle composition must contain no unrelated providers");
            Check(binding.State.Capture()["capabilities"].AsObject().Select(p=>p.Key).SequenceEqual(new[]{"liquid"}),"Bottle saves must contain only liquid state");
            Check(liquid.TryConsume(.25)&&liquid.Volume==.75&&!liquid.TryFill("oil",.1)&&liquid.Volume==.75,"Liquid transactions reject incompatible content without partial writes");
            Check(!liquid.TryConsume(2)&&!liquid.TryFill("water",2)&&!liquid.TryFill("water",double.NaN),"Liquid operations respect quantity limits");
            var bedType=new WorldObjectTypeDefinition{Capabilities=new(){new WorldInteractionCapability(),new WorldUsageCapacityCapability()}};
            var bed=WorldTestObjects.WithArea(new GameObject3D{Name="Bed",PersistentId="cap_bed",Components=new(WorldTestObjects.Components(bedType))});root.AddChild(bed);
            var capacity=bed.GetComponent<WorldUsageCapacityComponent3D>();
            Check(capacity.Reserve("a")&&!capacity.Reserve("b"),"Capacity provider enforces the type's slot count");
            var graph=new NpcGraphAsset();graph.Nodes.Add(new NpcRootNodeData());
            var actorHost=new GameObject3D{Name="Observer",PersistentId="cap_actor",Components=new(){new CharacterDataComponent3D{Profile=new(){FeatureLibrary=new()},Persist=false},new AiAgentComponent3D{Graph=graph,Persist=false,Running=false}}};root.AddChild(actorHost);
            var actor=actorHost.GetComponent<AiAgentComponent3D>();var facts=NpcBindingAdapters.Find("point").Capture(actor,new[]{"cap_bed"},false).ToDictionary(p=>p.Key,p=>p.Value);
            Check(facts["point.cap_bed.stock"].Kind==GoapValueKind.Unknown&&facts["point.cap_bed.available"].Kind==GoapValueKind.Boolean,"A bed exposes occupancy but no invented stock fact");
            var plainType=new WorldObjectTypeDefinition{Capabilities=new(){new WorldInteractionCapability()}};
            var plain=WorldTestObjects.WithArea(new GameObject3D{Name="Plain",PersistentId="cap_plain",Components=new(WorldTestObjects.Components(plainType))});root.AddChild(plain);
            facts=NpcBindingAdapters.Find("point").Capture(actor,new[]{"cap_plain"},false).ToDictionary(p=>p.Key,p=>p.Value);
            Check(facts["point.cap_plain.available"].Kind==GoapValueKind.Unknown,"No occupancy provider must not be treated as a free slot");
            Check(NpcBindingAdapters.Find("point").Acquire(actor,CharacterValue.Text("cap_plain"),"capacity")==null,"An absent capacity capability cannot grant a lease");
            Check(typeof(INpcBindingAdapter).GetMethod("Commit")==null,"NPC adapters cannot bypass shared interaction through direct stock writes");
            var saved=world.Capture();liquid.TryConsume(.25);world.Close();world.Restore(saved,world.SchemaVersion);
            Check(liquid.Volume==.75&&capacity.Reservations==0,"Deferred local capability types restore saved contents without retaining transient leases");
            Reject(()=>world.Restore(saved,1),"Old world save schemas must be rejected without compatibility branches");
            Reject(()=>world.Restore(saved,2),"Manual-record world saves must be rejected after placement identity migration");
            var stockType=WorldTestObjects.StockType(20);var record=new WorldObjectComponent3D{ObjectType=stockType,Overrides=new(){new WorldStockCapability{InitialAmount=50}}};
            WorldCatalog.ValidateOverrides(record);
            Check(record.Definition<WorldStockCapability>().InitialAmount==50&&stockType.Capability<WorldStockCapability>().InitialAmount==20,"Typed instance overrides do not mutate shared defaults");
            record.Overrides.Add(new WorldLiquidCapability());Reject(()=>WorldCatalog.ValidateOverrides(record),"An override cannot add a capability absent from its type");record.Overrides.RemoveAt(1);
            stockType.Capabilities.Add(new WorldStockCapability());Reject(()=>WorldCatalog.ValidateCapabilities(stockType.Capabilities),"Duplicate capability keys must be rejected");stockType.Capabilities.RemoveAt(stockType.Capabilities.Count-1);
            var detached=new WorldObjectComponent3D{ObjectType=bottleType};
            Reject(()=>detached.ValidateComponents(new Component3D[]{detached}),"Declared capability needs its matching scene provider");
            Reject(()=>binding.ValidateComponents(new Component3D[]{binding,new WorldLiquidComponent3D(),new WorldOpeningHoursComponent3D()}),"An undeclared provider is a configuration error");
#if TOOLS
            var copy=(WorldObjectComponent3D)record.Duplicate(true);copy.Definition<WorldStockCapability>().InitialAmount=9;
            Check(record.Definition<WorldStockCapability>().InitialAmount==50,"Duplicated scene configuration must deep-copy overrides");
            var syncHost=new GameObject3D{Components=new(){new WorldObjectComponent3D{ObjectType=bottleType},new WorldInteractionComponent3D()}};
            var synced=WorldObjectAuthoring.SynchronizeHost(syncHost.Components.OfType<WorldObjectComponent3D>().Single(),syncHost);
            Check(syncHost.Components.OfType<WorldLiquidComponent3D>().Count()==1&&!syncHost.Components.OfType<WorldInteractionComponent3D>().Any(),"Authoring synchronization adds required providers and removes undeclared ones");syncHost.Free();
#endif
            root.Free();ModuleSystem.Shutdown();GD.Print($"WORLD_CAPABILITIES_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());if(GodotObject.IsInstanceValid(root))root.Free();ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
}
