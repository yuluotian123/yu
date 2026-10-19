using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic;

[Tool,GlobalClass]
public partial class CharacterNeedsDefinition : CharacterDefinitionResource
{
    public const string CommonPresetPath = "res://assets/resources/character/presets/common_needs.tres";
    [Export] public Godot.Collections.Array<CharacterNeedDefinition> Needs {get;set;}=new();
    public override string Category=>"需求集合";

    public Godot.Collections.Array<CharacterNeedDefinition> MergePreset(CharacterNeedsDefinition preset)
    {
        if (preset == null) throw new ArgumentException("请选择需求预设。");
        var ids = new HashSet<string>(Needs.Where(n => n != null).Select(n => n.NeedId), StringComparer.Ordinal);
        var presetIds = new HashSet<string>(StringComparer.Ordinal);
        var result = new Godot.Collections.Array<CharacterNeedDefinition>(Needs);
        foreach (var need in preset.Needs)
        {
            if (need == null || string.IsNullOrWhiteSpace(need.NeedId) || !presetIds.Add(need.NeedId))
                throw new ArgumentException("需求预设含有空项、空 ID 或重复 ID。");
            if (ids.Add(need.NeedId)) result.Add((CharacterNeedDefinition)need.Duplicate(true));
        }
        return result;
    }

    public Godot.Collections.Array<CharacterNeedDefinition> AddCustomNeed()
    {
        var ids = Needs.Where(n => n != null).Select(n => n.NeedId).ToHashSet(StringComparer.Ordinal);
        string id = "custom_need";
        for (int i = 2; ids.Contains(id); i++) id = "custom_need_" + i;
        var result = new Godot.Collections.Array<CharacterNeedDefinition>(Needs);
        result.Add(new() { NeedId = id, DisplayName = "自定义需求" });
        return result;
    }
}
