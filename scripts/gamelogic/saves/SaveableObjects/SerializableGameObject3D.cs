using Framework;
using Godot;

namespace GameLogic
{
    /// <summary>
    /// Generic 3D gameplay host that auto-registers a serialization component.
    /// </summary>
    public partial class SerializableGameObject3D : GameObject3D
    {
        private SerializationComponent3D _serializationComponent;

        public override void _Ready()
        {
            base._Ready();

            RootModule.Instance.GameState.RegisterSeriableGameObject(this);

            _serializationComponent = AddComponent<SerializationComponent3D>();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            RootModule.Instance.GameState.UnregisterSeriableGameObject(PersistentId);
        }

        public SerializableGameObjectData3D Save()
        {
            return _serializationComponent?.Save();
        }

        public SerializableGameObject3D CreateFromData(SerializableGameObjectData3D data, Node root = null)
        {
            if (data == null)
                return null;

            if (root == null)
                root = RootModule.Instance;

            PersistentId = data.PersistentId;

            root.AddChild(this);

            if (IsInsideTree())
                _serializationComponent?.Load(data);
            else
                Debugger.Warn("Failed to create SerializableGameObject3D from data: not inside scene tree after adding to root.");

            return this;
        }
    }
}
