#if TOOLS
using System;
using System.Linq;
using GameLogic;
using Godot;

public partial class WorldAuthoringValidation : Node
{
    const string Folder="res://.godot/world-validation/authoring/";
    int _checks;
    Node _undoRoot,_undoPlacement;
    public void RemovePlacement()=>_undoRoot.RemoveChild(_undoPlacement);
    public void RestorePlacement()=>_undoRoot.AddChild(_undoPlacement);
    void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);_checks++;}
    static void Save(Node root,string path)
    {
        var packed=new PackedScene();if(packed.Pack(root)!=Error.Ok||ResourceSaver.Save(packed,path)!=Error.Ok)throw new Exception("Save failed: "+path);
    }
    static Node Load(string path)=>ResourceLoader.Load<PackedScene>(path,cacheMode:ResourceLoader.CacheMode.Replace).Instantiate(PackedScene.GenEditState.Instance);
    static string Id(Node root,GameObject3D host)=>WorldIdentity.Resolve(root)[host];
    public override void _Ready()
    {
        try
        {
            WorldAuthoringCompiler.CompileAll();
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Folder));
            var type=new WorldObjectTypeDefinition{Id="authoring_type",Capabilities=new(){new WorldStockCapability{InitialAmount=10}}};
            ResourceSaver.Save(type,Folder+"type.tres");type.TakeOverPath(Folder+"type.tres");
            var template=new GameObject3D{Name="Crate",Components=new(){new WorldObjectComponent3D{ObjectType=type}}};
            Check(WorldAuthoringCompiler.Prepare(template),"Selecting a type automatically supplies its capability components");
            Check(!template.HasMeta(WorldIdentity.PlacementKey),"A template root must not carry a placement identity");
            Save(template,Folder+"crate.tscn");template.Free();
            var room=new Node3D{Name="Room"};var inner=(GameObject3D)Load(Folder+"crate.tscn");room.AddChild(inner);inner.Owner=room;
            Check(WorldAuthoringCompiler.Prepare(room),"A nested instance automatically receives a local placement key");
            Save(room,Folder+"room.tscn");WorldAuthoringCompiler.Forget(room);room.Free();
            var root=new Node3D{Name="Level"};var a=Load(Folder+"room.tscn");var b=Load(Folder+"room.tscn");
            root.AddChild(a);a.Owner=root;root.AddChild(b);b.Owner=root;
            WorldAuthoringCompiler.Prepare(root);var ah=(GameObject3D)a.GetChild(0);var bh=(GameObject3D)b.GetChild(0);
            string original=Id(root,ah),second=Id(root,bh);
            Check(original!=second,"Two room instances distinguish the same internal template object");
            a.Name="RenamedRoom";((Node3D)a).Position=new(2,3,4);WorldAuthoringCompiler.Prepare(root);
            Check(Id(root,ah)==original,"Renaming and moving a placement preserves its identity");
            var duplicate=a.Duplicate();root.AddChild(duplicate);duplicate.Owner=root;root.MoveChild(duplicate,0);
            WorldAuthoringCompiler.Prepare(root);
            Check(Id(root,ah)==original&&Id(root,(GameObject3D)duplicate.GetChild(0))!=original,"Duplication inserted before its original only changes the new placement");
            string dupId=Id(root,(GameObject3D)duplicate.GetChild(0));
            _undoRoot=root;_undoPlacement=duplicate;var undo=new UndoRedo();undo.CreateAction("Remove placement");undo.AddDoMethod(new Callable(this,nameof(RemovePlacement)));undo.AddUndoMethod(new Callable(this,nameof(RestorePlacement)));undo.CommitAction();
            WorldAuthoringCompiler.Prepare(root);undo.Undo();duplicate.Owner=root;WorldAuthoringCompiler.Prepare(root);
            Check(Id(root,(GameObject3D)duplicate.GetChild(0))==dupId,"Undo restores the same placement identity");
            undo.Redo();WorldAuthoringCompiler.Prepare(root);undo.Undo();duplicate.Owner=root;WorldAuthoringCompiler.Prepare(root);
            Check(Id(root,(GameObject3D)duplicate.GetChild(0))==dupId,"Redo followed by undo retains identity");
            undo.ClearHistory();undo.Dispose();
            Save(root,Folder+"level.tscn");WorldAuthoringCompiler.Forget(root);root.Free();root=(Node3D)Load(Folder+"level.tscn");
            Check(WorldIdentity.Resolve(root).Values.Contains(original)&&WorldIdentity.Resolve(root).Values.Contains(second)&&WorldIdentity.Resolve(root).Values.Contains(dupId),"Reopening preserves all nested placement identities");
            var place=new WorldLocationDefinition{Id="authoring_place"};var level=new WorldLevelDefinition{Id="authoring_level",ScenePath=Folder+"level.tscn",Location=place};
            var world=new WorldDefinition{Locations=new(){place},Levels=new(){level},ObjectTypes=new(){type}};
            WorldAuthoringCompiler.CompileAll(world);var catalog=new WorldCatalog(world);
            Check(catalog.Objects.Count==3&&catalog.Objects.Values.All(e=>e.Location.Id==place.Id),"Generated directory contains independent objects with inherited level location");
            var stateA=new WorldObjectState(catalog.Objects[original].Capabilities);var stateB=new WorldObjectState(catalog.Objects[second].Capabilities);
            stateA.Capability<WorldStockState>("stock").Amount=1;
            Check(stateB.Capability<WorldStockState>("stock").Amount==10,"Shared template configuration produces independent persistent state");
            string generated=FileAccess.GetFileAsString(level.CatalogPath);WorldAuthoringCompiler.CompileAll(world);
            Check(FileAccess.GetFileAsString(level.CatalogPath)==generated,"An unchanged save produces identical generated directory data");
            type.Capability<WorldStockCapability>().InitialAmount=22;ResourceSaver.Save(type,Folder+"type.tres");type.TakeOverPath(Folder+"type.tres");WorldAuthoringCompiler.CompileAll(world);
            Check(new WorldCatalog(world).Objects[original].Capability<WorldStockCapability>().InitialAmount==22,"Changing a type rebuilds dependent catalogs while preserving identity");
            Check(!FileAccess.GetFileAsString(Folder+"crate.tscn").Contains("_world_placement"),"Rebuilding levels does not write instance identity into the reusable template root");
            WorldAuthoringCompiler.Forget(root);root.Free();
            GD.Print($"WORLD_AUTHORING_OK: {_checks} checks");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
#endif
