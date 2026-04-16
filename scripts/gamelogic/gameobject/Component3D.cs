using Godot;
using System.Text.Json.Serialization;

namespace GameLogic
{
    [GlobalClass]
    public abstract partial class Component3D : Resource, IComponent
    {

        [JsonIgnore]
        public GameObject3D Owner { get; set; }

        [JsonIgnore]
        public abstract int Priority { get; }

        [JsonIgnore]
        public Node OwnerNode => Owner;

        [JsonIgnore]
        public Node3D OwnerNode3D => Owner;

        public virtual void OnInit() { }
        public virtual void OnUpdate(double delta) { }
        public virtual void OnPhysicsUpdate(double delta) { }
        public virtual void OnDestroy() { }

        public virtual Component3D Clone()
        {
            return (Component3D)Duplicate();
        }
    }
}
