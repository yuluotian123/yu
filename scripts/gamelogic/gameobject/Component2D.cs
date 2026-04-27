using Godot;
using System.Text.Json.Serialization;

namespace GameLogic
{
    [GlobalClass]
    public abstract partial class Component2D : Resource, IComponent
    {
        [JsonIgnore]
        public GameObject2D Owner { get; set; }

        [JsonIgnore]
        public abstract int Priority { get; }

        [JsonIgnore]
        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public Node OwnerNode => Owner;

        [JsonIgnore]
        public Node2D OwnerNode2D => Owner;

        public virtual void OnInit() { }
        public virtual void OnUpdate(double delta) { }
        public virtual void OnPhysicsUpdate(double delta) { }
        public virtual void OnDestroy() { }

        public virtual Component2D Clone()
        {
            return (Component2D)Duplicate();
        }
    }
}
