using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// 玩家相关的可存档纯数据对象。
/// </summary>
public class PlayerState
{
    [JsonInclude]
    private string _selectedUnitId = string.Empty;
    [JsonInclude]
    private List<PlayerUnitSnapshot> _ownedUnits = new();

    /// <summary>
    /// 获取当前玩家持有的单位快照列表。
    /// </summary>
    public IReadOnlyList<PlayerUnitSnapshot> OwnedUnits => _ownedUnits;
    /// <summary>
    /// 获取当前选中单位的唯一 ID。
    /// </summary>
    public string SelectedUnitId => _selectedUnitId;

    /// <summary>
    /// 用新的单位快照集合替换当前存档中的单位列表。
    /// </summary>
    public void ReplaceOwnedUnits(IEnumerable<PlayerUnitSnapshot> snapshots)
    {
        _ownedUnits.Clear();
        if (snapshots == null)
            return;

        foreach (var snapshot in snapshots)
        {
            if (snapshot != null)
                _ownedUnits.Add(snapshot);
        }
    }

    /// <summary>
    /// 设置当前选中单位的唯一 ID。
    /// </summary>
    public void SetSelectedUnitId(string unitId)
    {
        _selectedUnitId = unitId ?? string.Empty;
    }

    /// <summary>
    /// 清空当前选中单位信息。
    /// </summary>
    public void ClearSelectedUnit()
    {
        _selectedUnitId = string.Empty;
    }
}
