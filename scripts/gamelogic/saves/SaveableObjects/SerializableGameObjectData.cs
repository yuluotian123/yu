using System.Collections.Generic;
using Godot;

/// <summary>
/// Generic serialized snapshot for a serializable gameplay object and its components.
/// </summary>
public class SerializableGameObjectData2D
{
    public string PersistentId { get; set; } = string.Empty;
    public string OwnerStateJson { get; set; } = string.Empty;
    public Vector2 Position2D { get; set; } = Vector2.Zero;
    public float Rotation2D { get; set; }

    public List<SerializableComponentData> ComponentDatas { get; set; } = new();
}

/// <summary>
/// Generic serialized snapshot for a serializable 3D gameplay object and its components.
/// </summary>
public class SerializableGameObjectData3D
{
    public string PersistentId { get; set; } = string.Empty;
    public string OwnerStateJson { get; set; } = string.Empty;
    public Vector3 Position3D { get; set; } = Vector3.Zero;
    public Vector3 Rotation3D { get; set; } = Vector3.Zero;

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
