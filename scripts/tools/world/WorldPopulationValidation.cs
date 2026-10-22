using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using GameLogic.AI;
using Godot;

/// <summary>Deterministic local planning burst. Excludes drawing, animation and travel execution.</summary>
public partial class WorldPopulationValidation : Node
{
    public override async void _Ready()
    {
        try
        {
            ModuleSystem.RegisterModule<ISaveModule>(new SaveModule("res://.godot/world-validation/population-saves","res://.godot/world-validation/no-legacy"));
            var session=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            foreach(int population in new[]{16,64,128})await Measure(session,population);
            ModuleSystem.Shutdown();GD.Print("WORLD_POPULATION_OK: per-frame path/sight budgets and every actor completed all bursts");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());ModuleSystem.Shutdown();GetTree().Quit(1);}
    }
    async Task Measure(WorldModule session,int population)
    {
        var root=new GameObject3D{Name="PopulationWorld",ProcessMode=ProcessModeEnum.Disabled,Components=new(){new AiWorldComponent3D()}};AddChild(root);
        var world=root.GetComponent<AiWorldComponent3D>();
        var mesh=new NavigationMesh{Vertices=new[]{new Vector3(-100,0,-100),new Vector3(-100,0,100),new Vector3(100,0,100),new Vector3(100,0,-100)}};mesh.AddPolygon(new[]{0,1,2,3});
        root.AddChild(new NavigationRegion3D{NavigationMesh=mesh});
        for(int i=0;i<32;i++)root.AddChild(WorldTestObjects.WithArea(new GameObject3D{Name="Point"+i,PersistentId="population_point_"+i,Position=new(i%8*2,0,i/8*2),Components=new(WorldTestObjects.Components(WorldTestObjects.StockType()))}));
        var fixture=new NpcSelectorBindingValidation.Fixture();fixture.First.Results.AddRange(fixture.First.Predictions);fixture.Second.Results.AddRange(fixture.Second.Predictions);
        var actors=new List<AiAgentComponent3D>();
        for(int i=0;i<population;i++)
        {
            var host=new GameObject3D{Name="Actor"+i,PersistentId="population_actor_"+i,Position=new(i%8*.2f,.36f,-1-i/8*.2f),Components=new(){
                new CharacterDataComponent3D{Profile=fixture.Profile,Persist=false},new CharacterMovementComponent3D{Profile=new(),StartDisabled=true},
                new NpcNavigationComponent3D{EnableAvoidance=false},new AiAgentComponent3D{Graph=fixture.Graph,Persist=false}}};
            var body=new CharacterBody3D{Name="PhysicsBody",CollisionLayer=0,CollisionMask=0};body.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new(.4f,.72f,.4f)}});
            body.AddChild(new NavigationAgent3D{Name="NavigationAgent3D"});host.AddChild(body);root.AddChild(host);actors.Add(host.GetComponent<AiAgentComponent3D>());
        }
        for(int n=0;n<20&&actors.Any(a=>!a.Navigation.MapReady);n++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        if(actors.Any(a=>a.Program==null||!a.Navigation.MapReady))throw new InvalidOperationException("Population fixture initialization failed");
        var samples=new List<double>();long allocations=0;int paths=0,sights=0,frames=0;
        for(int round=0;round<3;round++)
        {
            foreach(var actor in actors)
            {
                actor.State.Apply(new[]{new KeyValuePair<string,CharacterValue>("private.started",CharacterValue.Bool(false)),new KeyValuePair<string,CharacterValue>("private.done",CharacterValue.Bool(false))});actor.RequestReplan();
            }
            int start=frames;
            while(actors.Any(a=>!a.State.Read("private.done").IsTrue))
            {
                if(frames-start>1200)throw new InvalidOperationException("Planning starvation: "+string.Join("; ",actors.Where(a=>!a.State.Read("private.done").IsTrue).Take(3).Select(a=>a.ActorId+" "+a.LastFailure)));
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();ModuleSystem.Process(1d/60,1d/60);
                foreach(var actor in actors){actor.Character.OnUpdate(1d/60);actor.OnUpdate(1d/60);}world.OnUpdate(1d/60);
                samples.Add(timer.Elapsed.TotalMilliseconds);allocations+=GC.GetAllocatedBytesForCurrentThread()-before;frames++;
                paths=Math.Max(paths,session.PathQueriesLastFrame);sights=Math.Max(sights,session.SightQueriesLastFrame);
                if(paths>session.PathQueriesPerFrame||sights>session.SightQueriesPerFrame)throw new InvalidOperationException("World query budget exceeded");
            }
        }
        samples.Sort();double Percentile(double p)=>samples[Math.Min(samples.Count-1,(int)Math.Ceiling(samples.Count*p)-1)];
        GD.Print($"WORLD_POPULATION: actors={population}, points=32, bursts=3, frames={frames}, cpu_p95_ms={Percentile(.95):0.000}, cpu_p99_ms={Percentile(.99):0.000}, path_peak={paths}, sight_peak={sights}, alloc_kib_per_frame={allocations/1024d/frames:0.0}");
        root.Free();await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
}
