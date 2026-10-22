using Godot;
namespace GameLogic;

public interface IWorldCapabilityProvider { string CapabilityKey {get;} }

[Tool, GlobalClass]
public abstract partial class WorldCapabilityComponent3D : Component3D,IWorldCapabilityProvider
{
    public override int Priority=>ComponentPriority.Interaction+10;
    public abstract string CapabilityKey {get;}
    public WorldObjectComponent3D Object {get;private set;}
    public override void OnInit()
    {
        Object=Owner.GetComponent<WorldObjectComponent3D>();
        WorldCatalog.Require(Object?.State!=null,"能力组件需要已绑定的 WorldObjectComponent3D。");
        WorldCatalog.Require(System.Linq.Enumerable.Any(Object.Definitions,d=>d.Key==CapabilityKey),"对象未声明能力："+CapabilityKey);
    }
    public override void OnDestroy()=>Object=null;
}
