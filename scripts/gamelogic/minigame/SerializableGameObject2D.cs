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

            _serializationComponent = AddComponent<SerializationComponent>();
        }


        public SerializationComponent GetSerializationComponent() => _serializationComponent;
    }
}
