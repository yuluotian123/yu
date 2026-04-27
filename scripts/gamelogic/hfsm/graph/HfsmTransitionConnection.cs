using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public class HfsmTransitionConnection : GraphConnection
    {
        public string TransitionName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public HfsmConditionUseMode UseMode { get; set; } = HfsmConditionUseMode.And;
        public List<HfsmConditionBase> Conditions { get; set; } = new();

        public override string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(TransitionName))
                return TransitionName;

            if (Conditions == null || Conditions.Count == 0)
                return "Always";

            if (Conditions.Count == 1)
                return Conditions[0]?.Description ?? "Transition";

            string joiner = UseMode == HfsmConditionUseMode.And ? " && " : " || ";
            return string.Join(joiner, Conditions.ConvertAll(condition => condition?.Description ?? "null"));
        }

        public bool CanUse(HfsmRuntime runtime)
        {
            if (Conditions == null || Conditions.Count == 0)
                return true;

            if (UseMode == HfsmConditionUseMode.Or)
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

            var useModeRow = new HBoxContainer();
            useModeRow.AddChild(new Label
            {
                Text = "Conditions",
                VerticalAlignment = VerticalAlignment.Center
            });

            var useOption = new OptionButton();
            useOption.AddItem("All (And)", (int)HfsmConditionUseMode.And);
            useOption.AddItem("Any (Or)", (int)HfsmConditionUseMode.Or);
            useOption.Selected = (int)UseMode;
            useOption.ItemSelected += index => UseMode = (HfsmConditionUseMode)index;
            useModeRow.AddChild(useOption);
            root.AddChild(useModeRow);

            root.AddChild(new HSeparator());

            var listControl = new ReorderableListControl<HfsmConditionBase>(
                items: Conditions,
                buildItemUi: condition => condition.CreateEditUI(context),
                getItemLabel: condition => condition.Description,
                availableTypes: SubTypeCache.GetSubTypes<HfsmConditionBase>(),
                factory: type => (HfsmConditionBase)System.Activator.CreateInstance(type)
            );

            root.AddChild(listControl.Build());
            return root;
        }
#endif
    }
}
