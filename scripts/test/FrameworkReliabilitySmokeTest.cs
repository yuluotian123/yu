using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Framework;
using GameLogic;
using Godot;
using RuntimeReliabilityTests;

public partial class FrameworkReliabilitySmokeTest : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            VerifyEvents();
            VerifyModuleRollback();
            VerifySaves();
            VerifyObjectPool();
            VerifyComponentLifecycle();
            await VerifyNodePool();
            await VerifyResourceCompletion();
            await VerifyPreloadCancellation();
            await VerifyPreloadSuccess();
            ModuleSystem.Shutdown();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("[FrameworkReliabilitySmokeTest] PASS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[FrameworkReliabilitySmokeTest] FAIL: {exception}");
            ModuleSystem.Shutdown();
            GetTree().Quit(1);
        }
    }

    private static void VerifyEvents()
    {
        var events = new EventModule();
        int received = 0;
        Action later = () => received++;
        Action failing = null;
        failing = () =>
        {
            events.Unsubscribe(1, failing);
            events.Subscribe(1, later);
            throw new InvalidOperationException("Expected handler failure.");
        };
        events.Subscribe(1, failing);
        Expect<InvalidOperationException>(() => events.Send(1));
        events.Send(1);
        Require(received == 1, "Throwing handler left pending mutations unapplied.");

        bool nested = false;
        var calls = new List<string>();
        Action second = () => calls.Add("second");
        Action added = () => calls.Add("added");
        events.Subscribe(2, () =>
        {
            calls.Add("first");
            if (nested) return;
            nested = true;
            events.Unsubscribe(2, second);
            events.Subscribe(2, added);
            Require(!events.Subscribe(2, added), "Duplicate pending handler was accepted.");
            events.Send(2);
        });
        events.Subscribe(2, second);
        events.Send(2);
        Require(string.Join(",", calls) == "first,first,second,second", "Nested send changed the active subscriber list.");
        calls.Clear();
        events.Send(2);
        Require(string.Join(",", calls) == "first,added", "Deferred subscription order is incorrect.");
        events.Subscribe<int>(3, _ => { });
        Expect<ArgumentException>(() => events.Subscribe<string>(3, _ => { }));
        Expect<ArgumentException>(() => events.Send(3, "wrong signature"));
        events.Shutdown();
    }

    private static void VerifyModuleRollback()
    {
        ModuleSystem.Shutdown();
        var existing = new ExistingModule();
        ModuleSystem.RegisterModule<IExistingModule>(existing);
        var failed = new FailingModule();
        Expect<InvalidOperationException>(() => ModuleSystem.RegisterModule<IFailingModule>(failed));
        Require(failed.ShutdownCount == 1 && ProbeDependency.ShutdownCount == 1,
            "Failed initialization did not clean up the candidate and its new dependency.");
        Require(ReferenceEquals(existing, ModuleSystem.GetModule<IExistingModule>()), "Rollback removed a preexisting module.");
        ModuleSystem.Process(0, 0);
        Require(existing.Ticks == 1 && failed.Ticks == 0, "Uninitialized module entered the update queue.");
        failed.ShouldFail = false;
        ModuleSystem.RegisterModule<IFailingModule>(failed);
        ModuleSystem.RegisterModule<IFailingModule>(failed);
        ModuleSystem.Process(0, 0);
        Require(failed.Ticks == 1 && ProbeDependency.InitCount == 2, "Retry or idempotent registration failed.");
        Expect<InvalidOperationException>(() => ModuleSystem.RegisterModule<IFailingModule>(new FailingModule()));
        Expect<InvalidOperationException>(() => ModuleSystem.GetModule<ICircularModule>());
        ModuleSystem.Shutdown();
        Require(existing.ShutdownCount == 1 && failed.ShutdownCount == 2, "Shutdown ran an unexpected number of times.");
        ModuleSystem.RegisterModule<IExistingModule>(new ExistingModule());
        ModuleSystem.Shutdown();
    }

    private static void VerifySaves()
    {
        string directory = ProjectSettings.GlobalizePath($"res://tmp/reliability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var saves = new SaveModule(directory, directory + "/legacy");
        var section = new CounterSection { Value = 11 };
        saves.RegisterSection(section);
        string primary = Path.Combine(directory, "slot.json");
        try
        {
            Expect<ArgumentException>(() => saves.Save("../default"));
            Require(!File.Exists(Path.Combine(directory, "default.json")), "Invalid slot overwrote the default save.");
            saves.Save("slot");
            section.Value = 22;
            saves.Save("slot");
            File.WriteAllText(primary, "{broken");
            section.Value = 0;
            Require(saves.Load("slot") && section.Value == 11, "Corrupt primary did not recover from backup.");
            section.Value = 33;
            saves.Save("slot");
            File.WriteAllText(primary, "null");
            Require(saves.Load("slot") && section.Value == 11, "Saving over corruption destroyed the valid backup.");
            File.WriteAllText(primary, "{\"meta\":{\"format_version\":2},\"legacy\":{},\"sections\":{\"bad\":null}}");
            Require(saves.Load("slot"), "Invalid section structure did not fall back.");
            saves.UnregisterSection(section);
            var lateSection = new CounterSection();
            saves.RegisterSection(lateSection);
            Require(lateSection.Value == 11, "Late section registration lost the recovered state.");
            Expect<InvalidOperationException>(() => saves.RegisterSection(new CounterSection()));
            File.WriteAllText(primary, "{\"meta\":{\"format_version\":999},\"legacy\":{},\"sections\":{}}");
            Require(!saves.Load("slot"), "Future save format was silently downgraded.");
            File.WriteAllText(primary, "{");
            File.WriteAllText(primary + ".bak", "[");
            Require(!saves.Load("slot"), "Two corrupt files should return false.");
            Require(Directory.GetFiles(directory, "*.tmp").Length == 0, "Save left a temporary file behind.");
        }
        finally
        {
            saves.Delete("slot");
            saves.Shutdown();
            Directory.Delete(directory);
        }
    }

    private static void VerifyObjectPool()
    {
        var pool = new ObjectPool<PoolItem>("test", 2, 0);
        var other = new ObjectPool<PoolItem>("other", 2, 0);
        var item = pool.Spawn();
        other.Recycle(item);
        pool.Recycle(item);
        pool.Recycle(item);
        Require(pool.Count == 1 && other.Count == 0 && item.Recycles == 1, "Pool accepted a duplicate or foreign return.");
        Require(ReferenceEquals(pool.Spawn(), item), "Pool did not reuse the returned object.");
        pool.Recycle(item);
        pool.Shutdown();
        other.Shutdown();
        Expect<InvalidOperationException>(() => pool.Spawn());
    }

    private void VerifyComponentLifecycle()
    {
        var actor2D = new GameObject2D();
        var probe2D = actor2D.AddComponent<LifecycleProbe2D>();
        AddChild(actor2D);
        actor2D.OnSpawn();
        Require(probe2D.Starts == 1, "2D component initialized twice on first spawn.");
        actor2D.OnRecycle();
        actor2D.OnRecycle();
        Require(probe2D.Stops == 1, "2D recycle destroyed a component twice.");
        actor2D.OnSpawn();
        Require(probe2D.Starts == 2, "2D reuse did not reinitialize.");
        Expect<InvalidOperationException>(() => actor2D.AddComponent<LifecycleProbe2D>());
        actor2D.OnRecycle();
        actor2D.Free();
        Require(probe2D.Stops == 2, "2D tree exit repeated pooled cleanup.");
        probe2D.Dispose();

        var actor3D = new GameObject3D();
        var probe3D = actor3D.AddComponent<LifecycleProbe3D>();
        AddChild(actor3D);
        actor3D.OnSpawn();
        actor3D.OnRecycle();
        actor3D.OnSpawn();
        actor3D.OnRecycle();
        actor3D.Free();
        Require(probe3D.Starts == 2 && probe3D.Stops == 2, "3D lifecycle does not match 2D lifecycle.");
        probe3D.Dispose();
    }

    private async Task VerifyNodePool()
    {
        using var scene = new PackedScene();
        var template = new Node2D { ProcessMode = ProcessModeEnum.Pausable };
        var body = new StaticBody2D { Name = "Body", DisableMode = CollisionObject2D.DisableModeEnum.KeepActive };
        template.AddChild(body);
        body.Owner = template;
        var shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(16, 16) } };
        body.AddChild(shape);
        shape.Owner = template;
        var timer = new Timer { Name = "Timer", ProcessMode = ProcessModeEnum.Always, Autostart = true, WaitTime = 0.1 };
        template.AddChild(timer);
        timer.Owner = template;
        Require(scene.Pack(template) == Error.Ok, "Could not pack test scene.");
        template.Free();
        var pool = new NodePool("test", scene, this, "test", 2, 0);
        var other = new NodePool("test", scene, this, "other", 2, 0);
        var node = (Node2D)pool.Spawn();
        node.Position = new Vector2(12345, 12345);
        await WaitFrames();
        using var query = new PhysicsPointQueryParameters2D { Position = node.GlobalPosition };
        Require(node.GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 1, "Active body is absent from physics.");
        other.Recycle(node);
        pool.Recycle(node);
        pool.Recycle(node);
        double remaining = node.GetNode<Timer>("Timer").TimeLeft;
        await WaitFrames();
        Require(pool.Count == 1 && other.Count == 0 && !node.Visible, "Node ownership/visibility is incorrect.");
        Require(node.GetNode<Timer>("Timer").TimeLeft == remaining, "Idle child with Always mode kept ticking.");
        Require(node.GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 0, "Idle body still participates in physics.");
        Require(ReferenceEquals(pool.Spawn(), node), "Node was not reused.");
        await WaitFrames();
        Require(node.Visible && node.ProcessMode == ProcessModeEnum.Pausable &&
            node.GetNode<Timer>("Timer").ProcessMode == ProcessModeEnum.Always &&
            node.GetNode<StaticBody2D>("Body").DisableMode == CollisionObject2D.DisableModeEnum.KeepActive,
            "Reuse did not restore original modes.");
        Require(node.GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 1, "Reused body did not rejoin physics.");
        pool.Recycle(node);
        pool.Shutdown();
        other.Shutdown();
        await WaitFrames();
    }

    private static async Task VerifyResourceCompletion()
    {
        var module = new ResourceModule();
        var handle = new ResourceHandle<Resource>("test", module);
        Task<ResourceHandle<Resource>> task = handle.Task;
        int calls = 0;
        handle.OnCompleted(_ => throw new InvalidOperationException("Expected callback failure."));
        handle.OnCompleted(_ => calls++);
        handle.SetFailedInternal("Expected load failure.");
        Require(task.IsCompleted && calls == 1, "Callback exception blocked another callback or task completion.");
        await task;
        handle.Dispose();
        handle.Dispose();
        Require(calls == 1, "Disposal invoked completion twice.");
        var releasing = new ResourceHandle<Resource>("release-in-callback", module);
        var releasingTask = releasing.Task;
        releasing.OnCompleted(value => value.Dispose());
        releasing.OnCompleted(_ => calls++);
        releasing.SetFailedInternal("Expected failure.");
        Require(releasingTask.IsCompleted && calls == 2, "Release from callback caused recursive notifications.");
    }

    private async Task VerifyPreloadCancellation()
    {
        var resource = new ResourceModule();
        var loader = new PendingLoader();
        resource.SetLoader(loader);
        ModuleSystem.RegisterModule<IResourceModule>(resource);
        var preload = new PreloadProcedure();
        var idle = new IdleProcedure();
        var fsm = Fsm<IProcedureModule>.Create("preload-test", new ProcedureModule(), preload, idle);
        fsm.Start<PreloadProcedure>();
        ResourceHandleBase first = loader.Last;
        fsm.ChangeState(typeof(IdleProcedure));
        fsm.ChangeState(typeof(PreloadProcedure));
        Task currentLoad = preload.PendingLoad;
        ResourceHandleBase second = loader.Last;
        first.SetFailedInternal("Late completion");
        await WaitFrames();
        Require(ReferenceEquals(fsm.CurrentState, preload) && !currentLoad.IsCompleted,
            "Old load completion affected the new procedure entry.");
        fsm.ChangeState(typeof(IdleProcedure));
        second.SetFailedInternal("Late completion");
        await WaitFrames();
        Require(currentLoad.IsCompleted && ReferenceEquals(fsm.CurrentState, idle) &&
            first.Status == ResourceHandleStatus.Released && second.Status == ResourceHandleStatus.Released,
            "Leaving preload did not release requests or suppressed completion incorrectly.");
        fsm.Shutdown();
        ModuleSystem.Shutdown();
    }

    private async Task WaitFrames()
    {
        for (int i = 0; i < 3; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task VerifyPreloadSuccess()
    {
        var root = new Node { Name = "Root" };
        root.AddChild(new Node { Name = "UICanvas" });
        GetTree().Root.AddChild(root);
        var resource = new ResourceModule();
        var loader = new PendingLoader();
        resource.SetLoader(loader);
        ModuleSystem.RegisterModule<IResourceModule>(resource);
        string unusedPath = ProjectSettings.GlobalizePath($"res://tmp/empty-save-{Guid.NewGuid():N}");
        ModuleSystem.RegisterModule<ISaveModule>(new SaveModule(unusedPath, unusedPath));
        var preload = new PreloadProcedure();
        var fsm = Fsm<IProcedureModule>.Create("preload-success", new ProcedureModule(),
            preload, new LevelProcedure(), new MainMenuProcedure());
        var scene = GD.Load<PackedScene>("res://assets/scenes/spacelevel.tscn");
        Require(scene != null, "Could not load the actual level scene.");
        fsm.Start<PreloadProcedure>();
        ResourceHandleBase handle = loader.Last;
        handle.SetSucceedInternal(scene);
        await preload.PendingLoad;
        Require(fsm.CurrentState is LevelProcedure && root.GetNodeOrNull<Node>("Spacelevel") != null,
            "Successful preload did not attach the level and advance the procedure.");
        var ids = new HashSet<string>();
        foreach (Node child in root.GetNode<Node>("Spacelevel").GetChildren())
            if (child is GameObject2D actor)
                Require(ids.Add(actor.PersistentId), $"Duplicate persistent ID in level: {actor.PersistentId}");
        Require(handle.IsValid, "Leaving preload released the transferred level handle.");
        fsm.Shutdown();
        root.Free();
        Require(handle.Status == ResourceHandleStatus.Released, "Level exit did not release the transferred handle.");
        ModuleSystem.Shutdown();
        await WaitFrames();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    public partial class LifecycleProbe2D : Component2D
    {
        public override int Priority => 0;
        public int Starts;
        public int Stops;
        public override void OnInit() => Starts++;
        public override void OnDestroy() => Stops++;
    }

    public partial class LifecycleProbe3D : Component3D
    {
        public override int Priority => 0;
        public int Starts;
        public int Stops;
        public override void OnInit() => Starts++;
        public override void OnDestroy() => Stops++;
    }

    private sealed class CounterSection : ISaveSection
    {
        public string SectionKey => "test";
        public string EntryKey => "counter";
        public int SchemaVersion => 1;
        public int Value;
        public JsonObject Capture() => new() { ["value"] = Value };
        public void Restore(JsonObject state, int schemaVersion) => Value = state["value"].GetValue<int>();
    }

    private sealed class PoolItem : IObjectPoolItem
    {
        public int Recycles;
        public void OnSpawn() { }
        public void OnRecycle() => Recycles++;
    }

    private sealed class IdleProcedure : ProcedureBase { }

    private sealed class PendingLoader : IResourceLoader
    {
        public ResourceHandleBase Last;
        public Resource LoadSync(string path) => null;
        public void RequestAsync(string path, ResourceHandleBase handle, string typeHint = "") => Last = handle;
        public void Tick(IResourceCache cache, bool enableLog = false) { }
        public ResourceLoaderProfilerSnapshot GetProfilerSnapshot() => new();
        public void Shutdown(string reason = null) { }
    }
}

namespace RuntimeReliabilityTests
{
    public interface IExistingModule { }
    public interface IFailingModule { }
    public interface IProbeDependency { }
    public interface ICircularModule { }

    public class ExistingModule : Framework.Module, IExistingModule, IProcessModule
    {
        public int Ticks;
        public int ShutdownCount;
        public override void OnInit() { }
        public override void Shutdown() => ShutdownCount++;
        public void Process(double delta, double realDelta) => Ticks++;
    }

    public class FailingModule : ExistingModule, IFailingModule
    {
        public bool ShouldFail = true;
        public override void OnInit()
        {
            ModuleSystem.GetModule<IProbeDependency>();
            if (ShouldFail) throw new InvalidOperationException("Expected initialization failure.");
        }
    }

    public class ProbeDependency : Framework.Module, IProbeDependency
    {
        public static int InitCount;
        public static int ShutdownCount;
        public override void OnInit() => InitCount++;
        public override void Shutdown() => ShutdownCount++;
    }

    public class CircularModule : Framework.Module, ICircularModule
    {
        public override void OnInit() => ModuleSystem.GetModule<ICircularModule>();
        public override void Shutdown() { }
    }
}
