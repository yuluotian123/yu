using Godot;

namespace Framework;

// F6 scene playtests bypass RootModule. Keep the same module loop available there.
internal partial class StandaloneModuleDriver : Node
{
    private static StandaloneModuleDriver _instance;
    public static void Ensure(SceneTree tree)
    {
        if (tree == null || RootModule.Instance != null || GodotObject.IsInstanceValid(_instance)) return;
        _instance = new StandaloneModuleDriver { Name = "StandaloneModules", ProcessPriority = -1000 };
        tree.Root.CallDeferred(Node.MethodName.AddChild, _instance);
    }

    public override void _Process(double delta)
    {
        if (RootModule.Instance == null) ModuleSystem.Process(delta, delta);
    }

    public override void _ExitTree()
    {
        if (RootModule.Instance == null) ModuleSystem.Shutdown();
        if (ReferenceEquals(_instance, this)) _instance = null;
    }
}
