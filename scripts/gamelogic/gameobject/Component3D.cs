using Godot;
using System.Text.Json.Serialization;

namespace GameLogic
{
    // Godot 的工具脚本标记不继承，具体组件也须显式添加 [Tool]。
    [Tool, GlobalClass]
    public abstract partial class Component3D : Resource, IComponent
    {

        [JsonIgnore]
        public GameObject3D Owner { get; set; }

        [JsonIgnore]
        public abstract int Priority { get; }

        [JsonIgnore]
        public bool IsActive { get; set; } = true;

        [JsonIgnore]
        public Node OwnerNode => Owner;

        [JsonIgnore]
        public Node3D OwnerNode3D => Owner;

        public virtual void OnInit() { }
        public virtual void OnUpdate(double delta) { }
        public virtual void OnPhysicsUpdate(double delta) { }
        public virtual void OnDestroy() { }
        // 编辑器只预览配置，不初始化模块、克隆运行时组件或推进世界时钟。
        public virtual void OnEditorUpdate(GameObject3D host) { }
        public virtual void OnEditorFrameUpdate(GameObject3D host, double delta) { }
        public virtual void OnEditorPreSave(GameObject3D host) { }
        public virtual void OnEditorExit(GameObject3D host) { }

        public virtual Component3D Clone()
        {
            return (Component3D)Duplicate();
        }
    }
}
