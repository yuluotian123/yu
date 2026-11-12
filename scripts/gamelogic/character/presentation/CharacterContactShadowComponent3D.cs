using Framework;
using Godot;

namespace GameLogic;

/// <summary>Ground contact under a billboard actor, independent of direct-light shadow softness.</summary>
[GlobalClass]
public partial class CharacterContactShadowComponent3D : Component3D
{
    public override int Priority => ComponentPriority.Movement - 1;
    [Export] public NodePath ShadowPath { get; set; } = new("ContactShadow");
    [Export] public NodePath BodyPath { get; set; } = new("PhysicsBody");
    [Export] public float FootOffset { get; set; } = -.468f;
    [Export(PropertyHint.Range, "0,1")] public float Strength { get; set; } = .65f;
    [Export] public Vector2 GroundSize { get; set; } = new(1.1f, .72f);
    [Export(PropertyHint.Range, "0.1,4")] public float FadeHeight { get; set; } = 1.8f;
    [Export(PropertyHint.Layers3DPhysics)] public uint GroundMask { get; set; } = 1;
    private Decal _shadow;
    private PhysicsRayQueryParameters3D _query;

    public override void OnInit()
    {
        _shadow = Owner.GetNodeOrNull<Decal>(ShadowPath);
        _query = new() { CollisionMask = GroundMask, CollideWithAreas = false };
        if (Owner.GetNodeOrNull<CollisionObject3D>(BodyPath) is {} body)
            _query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };
        if (_shadow != null) { _shadow.TopLevel = true; _shadow.Visible = false; }
    }

    public override void OnPhysicsUpdate(double delta)
    {
        if (!GodotObject.IsInstanceValid(_shadow) || !Owner.IsInsideTree()) return;
        Vector3 feet = Owner.GlobalPosition + Vector3.Up * FootOffset;
        _query.From = feet + Vector3.Up * .25f;
        _query.To = feet - Vector3.Up * (FadeHeight + .25f);
        using var space = Owner.GetWorld3D().DirectSpaceState;
        using var hit = space.IntersectRay(_query);
        if (hit.Count == 0) { _shadow.Visible = false; return; }
        Vector3 normal = hit["normal"].AsVector3();
        float height = Mathf.Max(0, (feet - hit["position"].AsVector3()).Dot(normal));
        float fade = Mathf.Clamp(height / Mathf.Max(.1f, FadeHeight), 0, 1);
        _shadow.Visible = normal.Dot(Vector3.Up) > .5f && fade < 1 && Owner.IsVisibleInTree();
        if (!_shadow.Visible) return;
        Vector3 tangent = Vector3.Right.Slide(normal).Normalized();
        _shadow.GlobalTransform = new(new Basis(tangent, normal, tangent.Cross(normal)), hit["position"].AsVector3());
        float spread = 1 + fade * .55f;
        _shadow.Size = new(GroundSize.X * spread, .2f, GroundSize.Y * spread);
        _shadow.Modulate = new Color(1, 1, 1, Strength * (1 - fade) * (1 - fade));
    }

    public override void OnDestroy()
    {
        if (GodotObject.IsInstanceValid(_shadow)) _shadow.Visible = false;
        _query?.Dispose(); _query = null; _shadow = null;
    }
}
