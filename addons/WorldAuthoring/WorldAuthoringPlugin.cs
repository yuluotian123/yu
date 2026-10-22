#if TOOLS
using System;
using Godot;
using GameLogic;
[Tool]
public partial class WorldAuthoringPlugin : EditorPlugin
{
    Node _root;
    bool _queued,_compiling;
    WorldCatalogExport _export;
    public override void _EnterTree()
    {
        SceneChanged+=OnSceneChanged;SceneSaved+=OnSaved;ResourceSaved+=OnResourceSaved;GetTree().NodeAdded+=OnAdded;
        _export=new();AddExportPlugin(_export);OnSceneChanged(EditorInterface.Singleton.GetEditedSceneRoot());
    }
    public override void _ExitTree()
    {
        SceneChanged-=OnSceneChanged;SceneSaved-=OnSaved;ResourceSaved-=OnResourceSaved;GetTree().NodeAdded-=OnAdded;
        WorldAuthoringCompiler.Forget(_root);if(_export!=null)RemoveExportPlugin(_export);
    }
    void OnSceneChanged(Node root){_root=root;QueuePrepare();}
    void OnAdded(Node node){if(_root!=null&&(_root==node||_root.IsAncestorOf(node)))QueuePrepare();}
    void QueuePrepare(){if(_queued)return;_queued=true;Callable.From(()=>{_queued=false;Prepare();}).CallDeferred();}
    void Prepare()
    {
        try{if(WorldAuthoringCompiler.Prepare(_root))EditorInterface.Singleton.MarkSceneAsUnsaved();}
        catch(Exception error){GD.PushError("世界对象配置："+error.Message);}
    }
    void OnSaved(string path){if(!_compiling)Callable.From(()=>Compile()).CallDeferred();}
    void OnResourceSaved(Resource resource)
    {if(resource is WorldObjectTypeDefinition or WorldCapabilityDefinition or WorldDefinition or WorldLevelDefinition or WorldLocationDefinition)OnSaved(resource.ResourcePath);}
    bool Compile()
    {
        if(_compiling)return true;_compiling=true;
        try{WorldAuthoringCompiler.CompileAll();return true;}
        catch(Exception error){GD.PushError("世界目录生成失败："+error.Message);return false;}
        finally{_compiling=false;}
    }
    public override void _ApplyChanges()=>Prepare();
    public override void _SaveExternalData(){Compile();}
    // Godot autosaves before build callbacks. Saving again here stops play and nests editor progress tasks.
    public override bool _Build(){Prepare();return Compile();}
}
[Tool]
public partial class WorldCatalogExport : EditorExportPlugin
{
    public override void _ExportBegin(string[] features,bool isDebug,string path,uint flags)
    {
        WorldAuthoringCompiler.CompileAll();
        var world=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
        foreach(var level in world.Levels)AddFile(level.CatalogPath,FileAccess.GetFileAsBytes(level.CatalogPath),false);
    }
}
#endif
