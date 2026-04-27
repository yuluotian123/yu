using System;
using System.Collections.Generic;
using Godot;

public sealed class GraphBlackboardKeyReference
{
    public string Key { get; set; } = string.Empty;
    public string ValueTypeName { get; set; } = string.Empty;

    public Control CreateEditUI(
        GraphEditorContext context,
        string labelText,
        IReadOnlyList<Type> allowedValueTypes = null)
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 4);

        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center
        });

        var candidates = CollectCandidates(context, allowedValueTypes);
        var option = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        option.AddItem("Select blackboard key...", 0);

        int selectedIndex = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            option.AddItem($"{candidate.Key} ({candidate.Scope}, {candidate.ValueDisplayName})", i + 1);
            if (string.Equals(candidate.Key, Key, StringComparison.Ordinal))
                selectedIndex = i + 1;
        }

        option.Selected = selectedIndex;
        row.AddChild(option);
        root.AddChild(row);

        var manualEdit = new LineEdit
        {
            Text = Key ?? string.Empty,
            PlaceholderText = "Manual key fallback",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(manualEdit);

        option.ItemSelected += index =>
        {
            int candidateIndex = (int)index - 1;
            if (candidateIndex < 0 || candidateIndex >= candidates.Count)
                return;

            Candidate candidate = candidates[candidateIndex];
            Key = candidate.Key;
            ValueTypeName = candidate.ValueTypeName;
            manualEdit.Text = Key;
        };

        manualEdit.TextChanged += value =>
        {
            Key = value;
            ValueTypeName = FindCandidate(candidates, value)?.ValueTypeName ?? string.Empty;
        };

        if (candidates.Count == 0)
        {
            var hint = new Label
            {
                Text = "No matching blackboard key found. You can still enter a runtime key manually.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            hint.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
            root.AddChild(hint);
        }

        return root;
    }

    private static Candidate FindCandidate(IReadOnlyList<Candidate> candidates, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i].Key, key, StringComparison.Ordinal))
                return candidates[i];
        }

        return null;
    }

    private static List<Candidate> CollectCandidates(
        GraphEditorContext context,
        IReadOnlyList<Type> allowedValueTypes)
    {
        var candidates = new List<Candidate>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        AddGraphEntries(candidates, seenKeys, "Local", context?.CurrentGraph, allowedValueTypes);

        if (context?.ParentGraphs != null)
        {
            for (int i = 0; i < context.ParentGraphs.Count; i++)
                AddGraphEntries(candidates, seenKeys, $"Parent {i + 1}", context.ParentGraphs[i], allowedValueTypes);
        }

        GraphBlackboardNode globalBlackboard = context?.GlobalBlackboard ?? GraphBlackboardNode.Current;
        AddEntries(candidates, seenKeys, "Global", globalBlackboard?.Entries, allowedValueTypes);
        return candidates;
    }

    private static void AddGraphEntries(
        List<Candidate> candidates,
        HashSet<string> seenKeys,
        string scope,
        GraphAsset graph,
        IReadOnlyList<Type> allowedValueTypes)
    {
        if (graph == null)
            return;

        AddEntries(candidates, seenKeys, scope, graph.BlackboardEntries, allowedValueTypes);
    }

    private static void AddEntries(
        List<Candidate> candidates,
        HashSet<string> seenKeys,
        string scope,
        IList<GraphBlackboardEntry> entries,
        IReadOnlyList<Type> allowedValueTypes)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            GraphBlackboardEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                continue;

            if (seenKeys.Contains(entry.Key) || !IsAllowed(entry.Value.ValueType, allowedValueTypes))
                continue;

            seenKeys.Add(entry.Key);
            candidates.Add(new Candidate
            {
                Key = entry.Key,
                Scope = scope,
                ValueDisplayName = entry.Value.DisplayName,
                ValueTypeName = entry.Value.ValueType.FullName ?? entry.Value.ValueType.Name
            });
        }
    }

    private static bool IsAllowed(Type valueType, IReadOnlyList<Type> allowedValueTypes)
    {
        if (allowedValueTypes == null || allowedValueTypes.Count == 0)
            return true;

        for (int i = 0; i < allowedValueTypes.Count; i++)
        {
            if (allowedValueTypes[i] == valueType)
                return true;
        }

        return false;
    }

    private sealed class Candidate
    {
        public string Key { get; set; }
        public string Scope { get; set; }
        public string ValueDisplayName { get; set; }
        public string ValueTypeName { get; set; }
    }
}
