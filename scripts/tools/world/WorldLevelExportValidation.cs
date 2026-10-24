#if TOOLS
using System;
using System.Linq;
using GameLogic;
using Godot;

public partial class WorldLevelExportValidation : Node
{
    const string Folder="res://.godot/world-validation/level-export/";
    int _checks;
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;GD.Print("EXPORT_CHECK: "+message);}
    static void Save(Node root,string path)
    {
        var packed=new PackedScene();WorldCatalog.Require(packed.Pack(root)==Error.Ok&&ResourceSaver.Save(packed,path)==Error.Ok,"Cannot save "+path);packed.TakeOverPath(path);
    }
    static void Child(Node root,Node parent,Node child){parent.AddChild(child);child.Owner=root;}
    static GameObject3D Create(string name,WorldLevelDefinition inherited=null)
    {
        var root=new GameObject3D{Name=name,Components=new(){new WorldLevelContextComponent3D{Level=inherited}}};
        var spawn=new GameObject3D{Name="Spawn",Components=new(){new WorldSpawnPointComponent3D{Entry="arrival"}}};Child(root,root,spawn);
        Child(root,spawn,new Marker3D{Name="Arrival",Position=new(1,0,2)});
        Save(root,Folder+name+".tscn");root.Free();
        return ResourceLoader.Load<PackedScene>(Folder+name+".tscn").Instantiate<GameObject3D>(PackedScene.GenEditState.Instance);
    }
    static WorldLevelContextComponent3D Context(GameObject3D root)=>root.Components.OfType<WorldLevelContextComponent3D>().Single();
    static GameObject3D Portal(GameObject3D root,string name,WorldLevelDefinition target,Vector3 position)
    {
        var host=new GameObject3D{Name=name,Position=position,Components=new(){new WorldTransitionComponent3D{TargetLevel=target,TargetEntry="arrival"}}};Child(root,root,host);
        var area=new Area3D{Name="Area3D"};Child(root,host,area);Child(root,area,new CollisionShape3D{Name="Shape",Shape=new BoxShape3D()});
        return host;
    }
    static string Export(GameObject3D root,WorldDefinition world)=>WorldLevelExporter.Export(root,Context(root),world,()=>Save(root,root.SceneFilePath));
    public override void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            var world=new WorldDefinition();ResourceSaver.Save(world,Folder+"world.tres");world.TakeOverPath(Folder+"world.tres");
            var a=Create("A");var b=Create("B");
            var type=new WorldObjectTypeDefinition{Capabilities=new(){new WorldStockCapability{InitialAmount=7}}};
            ResourceSaver.Save(type,Folder+"type.tres");type.TakeOverPath(Folder+"type.tres");
            var crate=new GameObject3D{Name="Crate",Components=new(){new WorldObjectComponent3D{ObjectType=type}}};Child(a,a,crate);
            var npc=ResourceLoader.Load<PackedScene>("res://assets/scenes/npc/resident.tscn").Instantiate<GameObject3D>(PackedScene.GenEditState.Instance);
            npc.PersistentId="export_resident";Child(a,a,npc);
            Check(Context(a).GetPropertyList().Any(p=>p["name"].AsString()==nameof(WorldLevelContextComponent3D.ExportConfiguration)&&p["type"].AsInt32()==(int)Variant.Type.Callable),"The component exposes a callable export button");
            Export(a,world);var la=Context(a).Level;string identity=la.Id;
            Check(world.Levels.Count==1&&world.Locations.Count==1&&world.InitialLevel==la&&la.DefaultEntry=="arrival","Export creates and binds the level, place, default entrance and world registration");
            Check(world.ObjectTypes.Single()==type&&new WorldCatalog(world).Objects.Count==1,"Referenced object types and placed objects are exported without manual registry edits");
            Check(new WorldCatalog(world).Actors.ContainsKey("export_resident"),"The same export includes the reusable NPC roster");
            Check(ResourceLoader.Load<PackedScene>(a.SceneFilePath,cacheMode:ResourceLoader.CacheMode.Ignore).GetState().GetNodeCount()>0,"Export saves the authored scene");
            Export(a,world);Check(world.Levels.Count==1&&world.Locations.Count==1&&Context(a).Level.Id==identity,"Repeated export keeps record identities and does not duplicate registration");
            Export(b,world);var lb=Context(b).Level;
            var pa=Portal(a,"Door",lb,new(4,0,3));Export(a,world);
            var forward=world.Routes.Single();string routeId=forward.Id;
            Check(!forward.Bidirectional&&forward.From==la&&forward.To==lb&&forward.ToEntry=="arrival"&&forward.TravelHours==0,"A source portal exports one zero-duration directed route");
            var routes=new WorldRoutes(new WorldCatalog(world));
            Check(routes.Find(la.Id,lb.Id,null)?.Legs.Count==1&&routes.Find(lb.Id,la.Id,null)==null,"A one-way portal never invents a return route");
            Check(forward.FromPosition==pa.Position&&forward.ToPosition==new Vector3(1,0,2),"NPC departure comes from the portal and arrival comes from its destination marker");
            var pb=Portal(b,"Return",la,new(-2,0,0));Export(b,world);
            Check(world.Routes.Count==2&&new WorldRoutes(new WorldCatalog(world)).Find(lb.Id,la.Id,null)!=null,"A real return portal exports the reverse journey");
            pa.Name="RenamedDoor";pa.Position=new(8,0,3);Save(a,a.SceneFilePath);WorldAuthoringCompiler.CompileAll(world);
            Check(world.Routes.Single(r=>r.From.Id==la.Id).Id==routeId&&world.Routes.Single(r=>r.From.Id==la.Id).FromPosition==pa.Position,"Normal scene saves update generated coordinates without changing the route identity");
            b.GetNode<Marker3D>("Spawn/Arrival").Position=new(6,0,2);Save(b,b.SceneFilePath);WorldAuthoringCompiler.CompileAll(world);
            Check(world.Routes.Single(r=>r.From.Id==la.Id).ToPosition==new Vector3(6,0,2),"Moving the destination entrance refreshes incoming routes");
            var duplicate=(GameObject3D)pa.Duplicate();Child(a,a,duplicate);a.MoveChild(duplicate,0);Export(a,world);
            Check(world.Routes.Count(r=>r.From.Id==la.Id)==2&&WorldIdentity.Resolve(a,true)[pa]!=WorldIdentity.Resolve(a,true)[duplicate]
                &&world.Routes.Any(r=>r.Id==routeId),"Copying a portal assigns a separate route and preserves the original identity");
            a.RemoveChild(duplicate);duplicate.Free();Export(a,world);
            Check(world.Routes.Count==2&&world.Routes.Any(r=>r.Id==routeId),"Deleting a portal removes only its generated route");
            var transition=pa.Components.OfType<WorldTransitionComponent3D>().Single();transition.Automatic=false;Export(a,world);
            Check(world.Routes.Count==1&&world.Routes[0].From.Id==lb.Id,"Event-controlled portals do not generate a route that bypasses their event conditions");
            transition.Automatic=true;transition.NpcTravelHours=.2;Export(a,world);
            Check(world.Routes.Single(r=>r.From.Id==la.Id).TravelHours==.2,"A portal can override the default NPC travel duration");
            transition.TargetEntry="missing";bool rejected=false;string[] before=world.Routes.Select(r=>r.Id).ToArray();
            try{Export(a,world);}catch(ArgumentException){rejected=true;}
            Check(rejected&&world.Routes.Select(r=>r.Id).SequenceEqual(before)&&!WorldLevelExporter.IsExporting,"Missing destination entrances fail without publishing a partial route set");
            transition.TargetEntry="arrival";Export(a,world);
            var room=new Node3D{Name="Room"};
            var doorTemplate=new GameObject3D{Name="ReusableDoor",Components=new(){new WorldTransitionComponent3D{TargetLevel=lb,TargetEntry="arrival"}}};
            var trigger=new Area3D{Name="Area3D"};Child(doorTemplate,doorTemplate,trigger);
            Save(doorTemplate,Folder+"door.tscn");doorTemplate.Free();
            var nested=ResourceLoader.Load<PackedScene>(Folder+"door.tscn").Instantiate(PackedScene.GenEditState.Instance);Child(room,room,nested);
            Save(room,Folder+"room.tscn");room.Free();
            for(int n=0;n<2;n++)Child(a,a,ResourceLoader.Load<PackedScene>(Folder+"room.tscn").Instantiate(PackedScene.GenEditState.Instance));
            Export(a,world);var nestedIds=world.Routes.Where(r=>r.From.Id==la.Id).Select(r=>r.Id).Order().ToArray();
            Check(nestedIds.Length==3&&nestedIds.Distinct().Count()==3,"Two instances of a room containing the same portal template generate independent routes");
            Export(a,world);
            Check(world.Routes.Where(r=>r.From.Id==la.Id).Select(r=>r.Id).Order().SequenceEqual(nestedIds),"Repeated export keeps nested Scene portal identities stable");
            var manual=new WorldRouteDefinition{From=la,To=lb,FromEntry="arrival",ToEntry="arrival",TravelHours=.75,Public=false,Enabled=false};world.Routes.Add(manual);Export(a,world);
            Check(world.Routes.Count==1&&world.Routes[0]==manual&&manual.TravelHours==.75&&!manual.Public&&!manual.Enabled,"Existing hand-authored directions, timing, knowledge and closed state are not bypassed by generated routes");
            var clone=Create("Copy",la);Export(clone,world);
            Check(Context(clone).Level.Id!=la.Id&&la.ScenePath==a.SceneFilePath&&world.Levels.Count==3,"Saving an inherited level as a new scene does not retarget the original level record");
            var shared=Create("Shared");var originalContext=Context(a);shared.Components=new(){originalContext};Export(shared,world);
            Check(Context(shared)!=originalContext&&Context(a).Level==la&&Context(shared).Level!=la,"Exporting a new placement does not mutate a shared context component from another Scene");
            var saved=ResourceLoader.Load<WorldDefinition>(world.ResourcePath,cacheMode:ResourceLoader.CacheMode.Ignore);
            Check(saved.Levels.Count==4&&saved.Routes.Count==1&&saved.Routes[0].Id==manual.Id,"World registration and route preservation survive resource reload");
            string unchanged=FileAccess.GetFileAsString(world.ResourcePath);Export(a,world);
            Check(FileAccess.GetFileAsString(world.ResourcePath)==unchanged,"Unchanged repeated exports serialize identically");
            WorldAuthoringCompiler.Forget(a);WorldAuthoringCompiler.Forget(b);WorldAuthoringCompiler.Forget(clone);WorldAuthoringCompiler.Forget(shared);a.Free();b.Free();clone.Free();shared.Free();
            GD.Print($"WORLD_LEVEL_EXPORT_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
#endif
