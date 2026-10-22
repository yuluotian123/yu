using System.Linq;
using Framework;
using Godot;
namespace GameLogic;

/// <summary>One authoritative entry for player input and NPC interaction requests.</summary>
public static class WorldInteraction
{
    public static WorldInteractionResult Check(GameObject3D actor,WorldInteractionComponent3D point,string operationId)
    {
        if(!GodotObject.IsInstanceValid(actor)||!actor.IsInsideTree()||string.IsNullOrWhiteSpace(actor.PersistentId))return WorldInteractionResult.Fail("交互角色无效。");
        var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
        if(world.IsSwitching)return WorldInteractionResult.Fail("正在切换关卡。");
        if(!GodotObject.IsInstanceValid(point)||!point.IsActive||!GodotObject.IsInstanceValid(point.Owner)||!point.Owner.IsInsideTree()||point.Object==null
            ||world.ResolvePoint(point.PointId)!=point||world.ResolveObject(point.PointId)!=point.Object||actor.GetTree()!=point.Owner.GetTree()
            ||world.LevelFor(actor)?.Id!=world.LevelFor(point.Owner)?.Id)return WorldInteractionResult.Fail("目标不在当前关卡或已卸载。");
        var target=point.Object;
        if(!target.IsAvailable)return WorldInteractionResult.Fail("对象已关闭、停用或销毁。");
        if(!target.CanUse(actor.PersistentId))return WorldInteractionResult.Fail("没有使用权限。");
        var operation=point.Operation(operationId);
        if(operation==null)return WorldInteractionResult.Fail("对象没有提供此操作。");
        if(!point.ContainsPosition(actor.GlobalPosition))return WorldInteractionResult.Fail("请进入对象的交互区域。");
        if(!HasLineOfSight(actor,point))return WorldInteractionResult.Fail("交互目标被遮挡。");
        foreach(var key in operation.RequiredCapabilities)
            if(!target.Owner.GetAllComponents().OfType<WorldCapabilityComponent3D>().Any(c=>c.CapabilityKey==key&&c.IsActive))return WorldInteractionResult.Fail("所需能力不可用："+key);
        if(operation.RequireCapacity&&target.Owner.GetComponent<WorldUsageCapacityComponent3D>()?.Available(actor.PersistentId)!=true)return WorldInteractionResult.Fail("对象正在被其他角色使用。");
        return operation.Check(new(actor,target));
    }
    public static WorldInteractionResult Execute(GameObject3D actor,WorldInteractionComponent3D point,string operationId)
    {
        var check=Check(actor,point,operationId);if(!check.Success)return check;
        var operation=point.Operation(operationId);var capacity=operation.RequireCapacity?point.Owner.GetComponent<WorldUsageCapacityComponent3D>():null;
        // An NPC action may already hold this slot. Do not release its longer-lived claim.
        bool alreadyReserved=capacity?.Reserved(actor.PersistentId)==true;
        if(capacity!=null&&!capacity.Reserve(actor.PersistentId))return WorldInteractionResult.Fail("对象正在被其他角色使用。");
        try{return operation.Apply(new(actor,point.Object));}
        finally{if(capacity!=null&&!alreadyReserved)capacity.Release(actor.PersistentId);}
    }
    public static bool HasLineOfSight(GameObject3D actor,WorldInteractionComponent3D point)
    {
        if(!point.RequireLineOfSight)return true;
        Vector3 from=actor.GlobalPosition+Vector3.Up*.5f,to=point.UsePosition+Vector3.Up*.5f;
        if(from.DistanceSquaredTo(to)<.0001f)return true;
        var excluded=new Godot.Collections.Array<Rid>();
        foreach(var body in actor.FindChildren("*","CollisionObject3D",true,false).OfType<CollisionObject3D>())excluded.Add(body.GetRid());
        var query=PhysicsRayQueryParameters3D.Create(from,to,point.OcclusionMask,excluded);
        query.HitFromInside=true;
        var hit=actor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count==0||hit["collider"].AsGodotObject() is Node node&&(node==point.Owner||point.Owner.IsAncestorOf(node));
    }
}
