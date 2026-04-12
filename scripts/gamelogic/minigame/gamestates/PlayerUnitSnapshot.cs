using Godot;

/// <summary>
/// 玩家单位的可序列化快照。
/// </summary>
public class PlayerUnitSnapshot
{
    /// <summary>
    /// 获取或设置单位唯一 ID。
    /// </summary>
    public string UnitId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置单位配置 ID。
    /// </summary>
    public string UnitConfigId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置单位显示名称。
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置单位世界坐标。
    /// </summary>
    public Vector2 Position { get; set; } = Vector2.Zero;

    /// <summary>
    /// 获取或设置单位朝向。
    /// </summary>
    public float Rotation { get; set; }

    /// <summary>
    /// 获取或设置当前是否有移动目标。
    /// </summary>
    public bool HasMoveTarget { get; set; }

    /// <summary>
    /// 获取或设置当前移动目标点。
    /// </summary>
    public Vector2 MoveTarget { get; set; } = Vector2.Zero;
}
