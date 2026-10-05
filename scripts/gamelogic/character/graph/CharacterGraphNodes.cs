using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace GameLogic
{
    public enum CharacterLifecycleEvent
    {
        BeginPlay,
        Update,
        PhysicsUpdate,
        EndPlay
    }

    public sealed class CharacterInputEventContext
    {
        public string NodeId { get; init; } = string.Empty;
        public float Value { get; init; }
    }

    public class CharacterLifecycleEventNodeData : GraphNodeData
    {
        public CharacterLifecycleEvent Event { get; set; }

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "生命周期事件（Lifecycle Event）";
        public override string GetCategory() => "角色 / 事件";
        public override string GetDisplayName() => $"Event {Event}";
        public override Color GetNodeColor() => new(0.85f, 0.3f, 0.3f);
        public override int GetOutputCount() => 1;
        public override int GetOutputMaxConnections(int port) => -1;
        public override bool CanBePrime() => false;

        public override void CreateNodeUI(GraphEditorContext context)
        {
            context.GraphNode.AddChild(new Label { Text = Event.ToString(), HorizontalAlignment = HorizontalAlignment.Center });
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var option = new OptionButton();
            foreach (string name in Enum.GetNames<CharacterLifecycleEvent>())
                option.AddItem(name);
            option.Select((int)Event);
            option.ItemSelected += index =>
            {
                Event = (CharacterLifecycleEvent)index;
                context?.CurrentGraph?.MarkDirty();
#if TOOLS
                // Refresh after the option's input event; the node may have been
                // removed or the editor switched to another graph in the meantime.
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(context?.GraphNode) && !context.GraphNode.IsQueuedForDeletion())
                        GraphNodeViewBuilder.RefreshNodeUI(this, context.GraphNode, context);
                }).CallDeferred();
#endif
            };
            return option;
        }
    }

    public class CharacterAbilityNodeData : GraphNodeData, IFlowNode
    {
        public string AbilityId { get; set; } = string.Empty;
        public string AbilityResourcePath { get; set; } = string.Empty;

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "激活技能（Activate Ability）";
        public override string GetCategory() => "角色 / 技能";
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(AbilityId) ? "Ability" : AbilityId;
        public override Color GetNodeColor() => new(0.75f, 0.45f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 4;
        public override int GetInputMaxConnections(int port) => -1;
        public override string GetOutputPortName(int port) => port switch
        {
            0 => "Activated",
            1 => "Completed",
            2 => "Cancelled",
            _ => "Rejected"
        };
        public override bool CanBePrime() => false;

        public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
        {
            CharacterGraphRuntime graph = context.GetUserData<CharacterGraphRuntime>();
            AbilityActivationResult result = graph?.TryActivateAbility(this) ?? AbilityActivationResult.InvalidContext;
            var data = new AbilityNodeRuntimeData { Result = result };
            if (result == AbilityActivationResult.Activated)
            {
                data.Activation = new AbilityActivationHandle(context.GetUserData<GameObject2D>()?
                    .GetComponent<AbilitySystemComponent2D>()?.GetRuntime(AbilityId));
            }
            runtime.SetNodeData(Id, data);
            if (result == AbilityActivationResult.Activated) runtime.PropagateFromOutput(Id, 0);
        }

        public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
        public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
        {
            if (!runtime.TryGetNodeData<AbilityNodeRuntimeData>(Id, out AbilityNodeRuntimeData data))
            {
                completion = new NodeCompletion(3);
                return true;
            }
            if (data.Result != AbilityActivationResult.Activated)
            {
                completion = new NodeCompletion(3, data.Result.ToString());
                return true;
            }
            if (data.Activation?.State == AbilityActivationState.Running)
            {
                completion = default;
                return false;
            }
            bool cancelled = data.Activation?.State != AbilityActivationState.Completed;
            completion = new NodeCompletion(cancelled ? 2 : 1, data.Activation?.ReturnLabel ?? "Cancelled");
            return true;
        }
        public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) => runtime.ClearNodeData(Id);

        public override void Validate(GraphAsset graph, GraphValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(AbilityId))
                result.AddError("AbilityId is required.", Id);
        }

        public override void CreateNodeUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(190, 0) };
            root.AddChild(new Label { Text = GetDisplayName(), HorizontalAlignment = HorizontalAlignment.Center });
            context.GraphNode.AddChild(root);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var root = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
            var id = new LineEdit { Text = AbilityId, PlaceholderText = "Stable AbilityId" };
            id.TextChanged += value => AbilityId = value.Trim();
            root.AddChild(id);
#if TOOLS
            root.AddChild(new GraphResourcePathField(
                typeof(AbilityResource),
                AbilityResourcePath,
                path =>
                {
                    AbilityResourcePath = path;
                    AbilityResource ability = AbilityResource.LoadFromPath(path);
                    if (ability != null)
                        AbilityId = ability.AbilityId;
                },
                resource => resource is AbilityResource ability ? ability.DisplayName : null));

            var open = new Button { Text = "Open Ability Timeline" };
            open.Pressed += () =>
            {
                AbilityResource ability = AbilityResource.LoadFromPath(AbilityResourcePath);
                if (ability?.Graph == null)
                {
                    GD.PushWarning($"[CharacterAbilityNodeData] Ability resource '{AbilityResourcePath}' has no graph.");
                    return;
                }

                GraphPlugin plugin = GraphPlugin.Instance;
                if (plugin == null)
                {
                    GD.PushWarning("[CharacterAbilityNodeData] GraphPlugin is not available.");
                    return;
                }

                plugin.OpenGraphEditor(
                    ability.Graph,
                    ability,
                    ability.Graph.Nodes?.OfType<AbilityTimelineNodeData>().FirstOrDefault()?.Id);
            };
            root.AddChild(open);
#endif
            return root;
        }

        private sealed class AbilityNodeRuntimeData
        {
            public AbilityActivationResult Result;
            public AbilityActivationHandle Activation;
        }
    }
}
