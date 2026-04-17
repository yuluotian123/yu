using System.Collections.Generic;
using Godot;

/// <summary>
/// Generic serialized snapshot for a serializable gameplay object and its components.
/// </summary>
public class SerializableGameObjectData
{
    public string PersistentId { get; set; } = string.Empty;
    public string OwnerStateJson { get; set; } = string.Empty;

    public Vector2 Position2D { get; set; } = Vector2.Zero;

    public float Rotation2D { get; set; }

    public List<SerializableComponentData> ComponentDatas { get; set; } = new();
}

/// <summary>
/// Serialized data emitted by a single gameplay component.
/// </summary>
public class SerializableComponentData
{
    public string Key { get; set; } = string.Empty;

    public string StateJson { get; set; } = string.Empty;
}
