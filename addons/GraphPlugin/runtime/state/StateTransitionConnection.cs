using System.Collections.Generic;
using Godot;

public class StateTransitionConnection : GraphConnection
{
    public string TransitionName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool CompletionOnly { get; set; }
    public GraphConditionUseMode UseMode { get; set; } = GraphConditionUseMode.And;
    public List<StateConditionBase> Conditions { get; set; } = new();

    public override string GetDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(TransitionName))
            return TransitionName;

        if (Conditions == null || Conditions.Count == 0)
            return "Always";

        if (Conditions.Count == 1)
            return Conditions[0]?.Description ?? "Transition";

        string joiner = UseMode == GraphConditionUseMode.And ? " && " : " || ";
        return string.Join(joiner, Conditions.ConvertAll(condition => condition?.Description ?? "null"));
    }

    public virtual bool CanUse(StateGraphRuntime runtime)
    {
        if (Conditions == null || Conditions.Count == 0)
            return true;

        if (UseMode == GraphConditionUseMode.Or)
        {
            for (int i = 0; i < Conditions.Count; i++)
            {
                if (Conditions[i]?.IsMet(runtime) == true)
                    return true;
            }

            return false;
        }

        for (int i = 0; i < Conditions.Count; i++)
        {
            if (Conditions[i]?.IsMet(runtime) != true)
                return false;
        }

        return true;
    }

    public override Label CreateConnectionLabel()
    {
        var label = base.CreateConnectionLabel();
        label.Text = GetDisplayName();
        return label;
    }

#if TOOLS
    public override Control CreateEditUI(GraphEditorContext context)
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(360f, 0f) };

        var nameEdit = new LineEdit
        {
            PlaceholderText = "Transition name",
            Text = TransitionName
        };
        nameEdit.TextChanged += value => TransitionName = value;
        root.AddChild(nameEdit);

        var priorityRow = new HBoxContainer();
        priorityRow.AddChild(new Label
        {
            Text = "Priority",
            VerticalAlignment = VerticalAlignment.Center
        });

        var prioritySpin = new SpinBox
        {
            MinValue = -999,
            MaxValue = 999,
            Step = 1,
            Value = Priority,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        prioritySpin.ValueChanged += value => Priority = (int)value;
        priorityRow.AddChild(prioritySpin);
        root.AddChild(priorityRow);

        var completionOnlyCheck = new CheckBox
        {
            Text = "Completion only",
            ButtonPressed = CompletionOnly
        };
        completionOnlyCheck.Toggled += value => CompletionOnly = value;
        root.AddChild(completionOnlyCheck);

        var useModeRow = new HBoxContainer();
        useModeRow.AddChild(new Label
        {
            Text = "Conditions",
            VerticalAlignment = VerticalAlignment.Center
        });

        var useOption = new OptionButton();
        useOption.AddItem("All (And)", (int)GraphConditionUseMode.And);
        useOption.AddItem("Any (Or)", (int)GraphConditionUseMode.Or);
        useOption.Selected = (int)UseMode;
        useOption.ItemSelected += index => UseMode = (GraphConditionUseMode)index;
        useModeRow.AddChild(useOption);
        root.AddChild(useModeRow);

        root.AddChild(new HSeparator());

        var listControl = new ReorderableListControl<StateConditionBase>(
            items: Conditions,
            buildItemUi: condition => condition.CreateEditUI(context),
            getItemLabel: condition => condition.Description,
            availableTypes: SubTypeCache.GetSubTypes<StateConditionBase>(),
            factory: type => (StateConditionBase)System.Activator.CreateInstance(type)
        );

        root.AddChild(listControl.Build());
        return root;
    }
#endif
}
