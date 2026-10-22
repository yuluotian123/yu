using Godot;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldSpawnPointComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.Default;
    [Export] public string Entry {get;set;}="default";
    [Export] public NodePath MarkerPath {get;set;}=new("Arrival");
    public Transform3D Transform=>(Owner.GetNodeOrNull<Node3D>(MarkerPath)??Owner).GlobalTransform;
}
