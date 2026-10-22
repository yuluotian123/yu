using System;
using System.Linq;
using Godot;
using GameLogic.AI;
namespace GameLogic;

/// <summary>Shared interaction volume and approach reference. Business state belongs to sibling capabilities.</summary>
[Tool, GlobalClass]
public partial class WorldInteractionComponent3D : WorldCapabilityComponent3D
{
    public override int Priority=>ComponentPriority.Interaction;
    public override string CapabilityKey=>"interaction";
    [Export] public NodePath UsePositionPath {get;set;}=new("UsePosition");
    [Export] public NodePath InteractionAreaPath {get;set;}=new("InteractionArea");
    [Export] public bool RequireLineOfSight {get;set;}=true;
    [Export(PropertyHint.Layers3DPhysics)] public uint OcclusionMask {get;set;}=1;
    public string PointId=>Object?.ObjectId??"";
    public string DisplayName=>Object?.DisplayName??"";
    public string[] Tags=>Object?.Tags??Array.Empty<string>();
    public AiWorldComponent3D World {get;private set;}
    Area3D _area;
    CollisionShape3D[] _shapes=Array.Empty<CollisionShape3D>();
    public Vector3 UsePosition=>Owner.GetNodeOrNull<Node3D>(UsePositionPath)?.GlobalPosition??Owner.GlobalPosition;
    public Godot.Collections.Array<WorldInteractionDefinition> Operations=>Object?.Definition<WorldInteractionCapability>()?.Operations;
    public WorldInteractionDefinition Operation(string id)=>Operations?.FirstOrDefault(o=>o.Id==id);
    public bool ContainsPosition(Vector3 position)
    {
        return _area!=null&&_shapes.Any(s=>GodotObject.IsInstanceValid(s)&&!s.Disabled&&Inside(s.Shape,s.GlobalTransform.AffineInverse()*position));
    }
    public Aabb InteractionBounds
    {
        get
        {
            Aabb? bounds=null;
            foreach(var shape in _shapes.Where(s=>GodotObject.IsInstanceValid(s)&&!s.Disabled))
            {
                var size=ShapeSize(shape.Shape);var box=shape.GlobalTransform*new Aabb(-size*.5f,size);bounds=bounds?.Merge(box)??box;
            }
            return bounds??new Aabb(Owner.GlobalPosition,Vector3.Zero);
        }
    }
    static Vector3 ShapeSize(Shape3D shape)=>shape switch
    {
        BoxShape3D box=>box.Size,SphereShape3D sphere=>Vector3.One*sphere.Radius*2,
        CylinderShape3D cylinder=>new(cylinder.Radius*2,cylinder.Height,cylinder.Radius*2),
        CapsuleShape3D capsule=>new(capsule.Radius*2,capsule.Height,capsule.Radius*2),_=>Vector3.Zero
    };
    static bool Inside(Shape3D shape,Vector3 position)=>shape switch
    {
        BoxShape3D box=>position.Abs().X<=box.Size.X*.5f&&position.Abs().Y<=box.Size.Y*.5f&&position.Abs().Z<=box.Size.Z*.5f,
        SphereShape3D sphere=>position.LengthSquared()<=sphere.Radius*sphere.Radius,
        CylinderShape3D cylinder=>Math.Abs(position.Y)<=cylinder.Height*.5f&&new Vector2(position.X,position.Z).LengthSquared()<=cylinder.Radius*cylinder.Radius,
        CapsuleShape3D capsule=>new Vector3(position.X,Math.Max(0,Math.Abs(position.Y)-(capsule.Height*.5f-capsule.Radius)),position.Z).LengthSquared()<=capsule.Radius*capsule.Radius,
        _=>false
    };
    public void ValidatePlacement(GameObject3D host)
    {
        WorldCatalog.Require(UsePositionPath.IsEmpty||host.GetNodeOrNull<Node3D>(UsePositionPath)!=null,"交互对象缺少已指定的 UsePosition 节点："+host.Name);
        var area=host.GetNodeOrNull<Area3D>(InteractionAreaPath);
        WorldCatalog.Require(area!=null,"交互对象缺少 Area3D 交互区域："+host.Name);
        if(area!=null)
        {
            var shapes=area.GetChildren().OfType<CollisionShape3D>().ToArray();
            WorldCatalog.Require(shapes.Any(s=>!s.Disabled),"交互区域需要启用的 CollisionShape3D："+host.Name);
            foreach(var shape in shapes){var size=ShapeSize(shape.Shape);WorldCatalog.Require(size.IsFinite()&&size.X>0&&size.Y>0&&size.Z>0,"交互区域仅支持有效的 Box、Sphere、Cylinder、Capsule 形状。");}
        }
    }
    public override void OnInit(){base.OnInit();ValidatePlacement(Owner);_area=Owner.GetNodeOrNull<Area3D>(InteractionAreaPath);_shapes=_area.GetChildren().OfType<CollisionShape3D>().ToArray();Register();}
    void Register(){World=AiWorldComponent3D.Find(Owner);World?.RegisterPoint(this);}
    public override void OnUpdate(double delta){if(World==null)Register();else World.UpdatePoint(this);}
    public override void OnDestroy(){World?.RemovePoint(this);World=null;_area=null;_shapes=Array.Empty<CollisionShape3D>();base.OnDestroy();}
}
