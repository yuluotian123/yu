using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace GameLogic
{
    public partial class SerializationComponent3D : Component3D
    {
        public override int Priority => int.MinValue;

        public SerializableGameObjectData3D Save()
        {
            var snapshot = new SerializableGameObjectData3D
            {
                PersistentId = Owner?.PersistentId ?? string.Empty,
                OwnerStateJson = SerializeOwnerState(),
            };

            if (Owner != null)
            {
                snapshot.Position3D = Owner.GlobalPosition;
                snapshot.Rotation3D = Owner.GlobalRotation;
            }

            var components = Owner?.GetAllComponents();
            if (components == null)
                return snapshot;

            for (int i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null || ReferenceEquals(component, this))
                    continue;

                var state = AutoStateSerializer.SerializeObject(component, includePublicProperties: false);
                if (state.Count == 0)
                    continue;

                snapshot.ComponentDatas.Add(new SerializableComponentData
                {
                    Key = component.GetType().FullName ?? component.GetType().Name,
                    StateJson = state.ToJsonString(),
                });
            }

            return snapshot;
        }

        public void Load(SerializableGameObjectData3D snapshot)
        {
            if (snapshot == null)
                return;

            var ownerState = ParseState(snapshot.OwnerStateJson);
            if (ownerState != null && OwnerNode != null)
                AutoStateSerializer.DeserializeInto(OwnerNode, ownerState, includePublicProperties: false);

            Owner.GlobalPosition = snapshot.Position3D;
            Owner.GlobalRotation = snapshot.Rotation3D;

            var snapshotLookup = new Dictionary<string, SerializableComponentData>(StringComparer.Ordinal);
            if (snapshot.ComponentDatas != null)
            {
                for (int i = 0; i < snapshot.ComponentDatas.Count; i++)
                {
                    var componentSnapshot = snapshot.ComponentDatas[i];
                    if (componentSnapshot == null || string.IsNullOrEmpty(componentSnapshot.Key))
                        continue;

                    snapshotLookup[componentSnapshot.Key] = componentSnapshot;
                }
            }

            var components = Owner?.GetAllComponents();
            if (components != null)
            {
                for (int i = 0; i < components.Count; i++)
                {
                    var component = components[i];
                    if (component == null || ReferenceEquals(component, this))
                        continue;

                    var key = component.GetType().FullName ?? component.GetType().Name;
                    if (!snapshotLookup.TryGetValue(key, out var componentSnapshot))
                        continue;

                    var componentState = ParseState(componentSnapshot.StateJson);
                    if (componentState != null)
                        AutoStateSerializer.DeserializeInto(component, componentState);
                }
            }
        }

        private string SerializeOwnerState()
        {
            if (OwnerNode == null)
                return string.Empty;

            var state = AutoStateSerializer.SerializeObject(OwnerNode, includePublicProperties: false);
            return state.Count > 0 ? state.ToJsonString() : string.Empty;
        }

        private static JsonObject ParseState(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonNode.Parse(json)?.AsObject();
        }
    }
}
