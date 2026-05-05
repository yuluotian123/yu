using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public class HfsmTransitionConnection : StateTransitionConnection
    {
        public bool CanUse(HfsmRuntime runtime)
        {
            return base.CanUse(runtime);
        }

        public override bool CanUse(StateGraphRuntime runtime)
        {
            return base.CanUse(runtime);
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

            var listControl = new ReorderableListControl<GraphConditionBase>(
                items: Conditions,
                buildItemUi: condition => condition.CreateEditUI(context),
                getItemLabel: condition => condition.Description,
                availableTypes: SubTypeCache.GetSubTypes<HfsmConditionBase>(),
                factory: type => (GraphConditionBase)System.Activator.CreateInstance(type)
            );

            root.AddChild(listControl.Build());
            return root;
        }
#endif
    }
}
