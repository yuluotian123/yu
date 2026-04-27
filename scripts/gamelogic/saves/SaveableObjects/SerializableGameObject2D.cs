using System;
using System.Text.Json.Serialization;
using Framework;
using Godot;

namespace GameLogic
{

    /// <summary>
    /// Generic 2D gameplay host that auto-registers a serialization component.
    /// </summary>
    public partial class SerializableGameObject2D : GameObject2D
    {
        private SerializationComponent2D _serializationComponent;


        public override void _Ready()
        {
            base._Ready();

            RootModule.Instance.GameState.RegisterSeriableGameObject(this);

            _serializationComponent = AddComponent<SerializationComponent2D>();
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            RootModule.Instance.GameState.UnregisterSeriableGameObject(PersistentId);
        }

        public SerializableGameObjectData2D Save()
        {
            return _serializationComponent?.Save();
        }


        /// <summary>
        /// 从数据中恢复Serializeablegameobject2D实例的标准方法，用其他方式恢复会导致未知风险
        /// </summary>
        /// <param name="data"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        public SerializableGameObject2D CreateFromData(SerializableGameObjectData2D data, Node root = null)
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
                Debugger.Warn($"Failed to create SerializableGameObject2D from data: not inside scene tree after adding to root.");

            return this;
        }
    }
}
