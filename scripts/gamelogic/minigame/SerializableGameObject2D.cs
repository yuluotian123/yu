using System;
using System.Text.Json.Serialization;
using Godot;

namespace GameLogic
{

    /// <summary>
    /// Generic 2D gameplay host that auto-registers a serialization component.
    /// </summary>
    public partial class SerializableGameObject2D : GameObject2D
    {
        private SerializationComponent _serializationComponent;

        public override void _Ready()
        {
            base._Ready();
            RootModule.Instance.GameState.RegisterSeriableGameObject(this);

            _serializationComponent = AddComponent<SerializationComponent>();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            RootModule.Instance.GameState.UnregisterSeriableGameObject(PersistentId);
        }


        public SerializationComponent GetSerializationComponent() => _serializationComponent;
    }
}
