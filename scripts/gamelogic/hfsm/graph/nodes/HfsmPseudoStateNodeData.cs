using System.Collections.Generic;
using Godot;

namespace GameLogic
{
    public interface IHfsmPseudoNodeData : IStatePseudoNodeData
    {
    }

    public class HfsmAnyStateNodeData : AnyStateNodeData, IHfsmPseudoNodeData
    {
        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };
        public override string GetDisplayName() => "Any State";
        public override Color GetNodeColor() => new(0.95f, 0.62f, 0.24f);
        public override int GetInputCount() => 0;
        public override int GetOutputCount() => 1;
        public override bool CanBePrime() => false;

        public override bool CanTransitionFrom(IStateNodeData currentState)
        {
            return currentState is IHfsmStateNodeData hfsmState && CanTransitionFrom(hfsmState);
        }

        public bool CanTransitionFrom(IHfsmStateNodeData currentState)
        {
            if (currentState == null)
                return false;

            if (HfsmTagUtility.ContainsTag(IgnoredStateNames, currentState.StateName) ||
                HfsmTagUtility.ContainsTag(IgnoredStateNames, currentState.Id))
                return false;

            foreach (string tag in currentState.GetTags())
            {
                if (HfsmTagUtility.ContainsTag(IgnoredTags, tag))
                    return false;
            }

            return true;
        }

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            root.AddChild(new Label
            {
                Text = "Global transitions",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            int ignoredCount = HfsmTagUtility.ParseTags(IgnoredStateNames).Count + HfsmTagUtility.ParseTags(IgnoredTags).Count;
            if (ignoredCount > 0)
                root.AddChild(new Label { Text = $"Ignored {ignoredCount}", HorizontalAlignment = HorizontalAlignment.Center });

            context.GraphNode.AddChild(root);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(190f, 0f) };
            root.AddChild(new Label
            {
                Text = "Global transitions",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var ignoredStates = new LineEdit
            {
                PlaceholderText = "Ignore states",
                Text = IgnoredStateNames
            };
            ignoredStates.TextChanged += value => IgnoredStateNames = value;
            root.AddChild(ignoredStates);

            var ignoredTags = new LineEdit
            {
                PlaceholderText = "Ignore tags",
                Text = IgnoredTags
            };
            ignoredTags.TextChanged += value => IgnoredTags = value;
            root.AddChild(ignoredTags);

            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
            root.AddThemeConstantOverride("separation", 6);
            AddAnyStateFields(root);
            return root;
        }

        private void AddAnyStateFields(VBoxContainer root)
        {
            root.AddChild(new Label
            {
                Text = "Global transitions",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var ignoredStates = new LineEdit
            {
                PlaceholderText = "Ignore states",
                Text = IgnoredStateNames
            };
            ignoredStates.TextChanged += value => IgnoredStateNames = value;
            root.AddChild(ignoredStates);

            var ignoredTags = new LineEdit
            {
                PlaceholderText = "Ignore tags",
                Text = IgnoredTags
            };
            ignoredTags.TextChanged += value => IgnoredTags = value;
            root.AddChild(ignoredTags);
        }
    }

    public class HfsmReturnStateNodeData : StateReturnNodeData, IHfsmPseudoNodeData
    {
        public override List<string> GetGraphTypes() => new() { HfsmGraphAsset.GraphTypeName };
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(Label) ? "Return" : Label;
        public override Color GetNodeColor() => new(0.66f, 0.56f, 0.92f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override int GetInputMaxConnections(int port) => -1;
        public override int GetOutputMaxConnections(int port) => -1;
        public override bool CanBePrime() => false;

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(150f, 0f) };
            root.AddChild(new Label
            {
                Text = "Resolves immediately",
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipText = true
            });
            context.GraphNode.AddChild(root);
        }

        public override void CreateUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            var labelEdit = new LineEdit
            {
                PlaceholderText = "Return label",
                Text = Label
            };
            labelEdit.TextChanged += value => Label = value;
            root.AddChild(labelEdit);

            root.AddChild(new Label
            {
                Text = "Resolves immediately",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(260f, 0f) };
            root.AddThemeConstantOverride("separation", 6);

            var labelEdit = new LineEdit
            {
                PlaceholderText = "Return label",
                Text = Label
            };
            labelEdit.TextChanged += value =>
            {
                Label = value;
                if (context.GraphNode != null)
                    context.GraphNode.Title = GetDisplayName();
            };
            root.AddChild(labelEdit);

            root.AddChild(new Label
            {
                Text = "Resolves immediately",
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return root;
        }
    }
}
