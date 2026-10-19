using Godot;

namespace GameLogic;

/// <summary>EC owns scene capture; the compositor owns render-thread DDGI resources.</summary>
[Tool, GlobalClass]
public partial class DdgiComponent3D : Component3D, ISerializationListener
{
    [Export] public DdgiProfile Profile { get; set; }
    [Export] public NodePath GeometryRoot { get; set; } = new(".");
    [Export] public NodePath WorldEnvironmentPath { get; set; } = new("WorldEnvironment");
    public override int Priority => 0;

    private DdgiEffect _effect;
    private Compositor _compositor;
    private Compositor _originalCompositor;
    private WorldEnvironment _environment;
    private Node3D _geometry;
    private bool _resumeAfterReload;

    public override void OnInit()
    {
        _resumeAfterReload = false;
        if (_effect != null) return;
        _geometry = Owner.GetNodeOrNull<Node3D>(GeometryRoot);
        _environment = Owner.GetNodeOrNull<WorldEnvironment>(WorldEnvironmentPath);
        if (Profile == null || _geometry == null || _environment == null)
        {
            GD.PushError("[DDGI] Profile, GeometryRoot and WorldEnvironmentPath are required.");
            return;
        }
        // Each component has its own cache, including when Environments are shared.
        _effect = new DdgiEffect();
        _originalCompositor = _environment.Compositor;
        _compositor = _originalCompositor != null ? (Compositor)_originalCompositor.Duplicate() : new Compositor();
        var effects = _compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        effects.Insert(0, _effect);
        _compositor.CompositorEffects = effects;
        _environment.Compositor = _compositor;
        OnUpdate(0);
    }

    public override void OnUpdate(double delta)
    {
        if (_effect == null && _resumeAfterReload && GodotObject.IsInstanceValid(Owner) && Owner.IsInsideTree())
            OnInit();
        if (_effect == null || Profile == null) return;
        using var settings = Profile.Snapshot();
        // A scheduling hint in world units; camera exposure never enters the GI cache.
        if (_environment?.Environment is Godot.Environment environment)
            settings["sky_energy"] = environment.BackgroundEnergyMultiplier * environment.BackgroundIntensity;
        _effect.CaptureScene(_geometry, settings);
    }

    public Godot.Collections.Dictionary GetStatus() => _effect?.GetStatus() ?? new();
    public CompositorEffect GetEffect() => _effect;

    public override void OnDestroy()
    {
        if (_effect == null) return;
        if (GodotObject.IsInstanceValid(_environment) && _environment.Compositor == _compositor)
            _environment.Compositor = _originalCompositor;
        _effect.ReleaseResources();
        // Other native callers may still retain this Resource for status/teardown.
        // GPU ownership is already released; let the shared resource expire normally.
        _effect = null;
        _compositor?.Dispose();
        _compositor = null;
        _environment = null;
        _geometry = null;
    }

    public void OnBeforeSerialize() => OnDestroy();

    public void OnAfterDeserialize()
    {
        _effect = null;
        _compositor = null;
        _originalCompositor = null;
        _environment = null;
        _geometry = null;
        _resumeAfterReload = true;
    }
}
