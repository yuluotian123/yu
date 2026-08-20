using System;
using System.Collections.Generic;
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
        public override string GetMenuName() => Event.ToString();
        public override string GetCategory() => "Character / Events";
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
            option.ItemSelected += index => Event = (CharacterLifecycleEvent)index;
            return option;
        }
    }

    public class CharacterAddMovementInputNodeData : GraphNodeData
    {
        public float Scale { get; set; } = 1f;

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Add Movement Input";
        public override string GetCategory() => "Character / Movement";
        public override Color GetNodeColor() => new(0.25f, 0.75f, 0.45f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override bool CanBePrime() => false;

        public override void Execute(GraphExecutionContext context)
        {
            float value = context.GetUserData<CharacterInputEventContext>()?.Value ?? 0f;
            context.GetUserData<GameObject2D>()?
                .GetComponent<CharacterMovementComponent2D>()?
                .AddMovementInput(value * Scale, ComponentPriority.Input);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var spin = new SpinBox { Value = Scale, MinValue = -100, MaxValue = 100, Step = 0.05 };
            spin.ValueChanged += value => Scale = (float)value;
            return spin;
        }
    }

    public class CharacterStopMovementInputNodeData : GraphNodeData
    {
        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Stop Movement Input";
        public override string GetCategory() => "Character / Movement";
        public override Color GetNodeColor() => new(0.25f, 0.75f, 0.45f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override bool CanBePrime() => false;

        public override void Execute(GraphExecutionContext context)
        {
            context.GetUserData<GameObject2D>()?
                .GetComponent<CharacterMovementComponent2D>()?
                .StopMovementInput(ComponentPriority.Input);
        }
    }

    public enum CharacterJumpCommand
    {
        RequestStart,
        SustainOn,
        SustainOff
    }

    public class CharacterJumpInputNodeData : GraphNodeData
    {
        public CharacterJumpCommand Command { get; set; }

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Jump Command";
        public override string GetCategory() => "Character / Movement";
        public override string GetDisplayName() => Command.ToString();
        public override Color GetNodeColor() => new(0.25f, 0.75f, 0.45f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override bool CanBePrime() => false;

        public override void Execute(GraphExecutionContext context)
        {
            CharacterMovementComponent2D movement = context.GetUserData<GameObject2D>()?
                .GetComponent<CharacterMovementComponent2D>();
            if (Command == CharacterJumpCommand.RequestStart)
                movement?.RequestJumpStart(ComponentPriority.Input);
            else
                movement?.SetJumpSustain(Command == CharacterJumpCommand.SustainOn, ComponentPriority.Input);
        }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var option = new OptionButton();
            foreach (string name in Enum.GetNames<CharacterJumpCommand>())
                option.AddItem(name);
            option.Select((int)Command);
            option.ItemSelected += index => Command = (CharacterJumpCommand)index;
            return option;
        }
    }

    public class CharacterSequenceNodeData : GraphNodeData, IFlowNode
    {
        public int Outputs { get; set; } = 2;

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Sequence";
        public override string GetCategory() => "Character / Flow";
        public override Color GetNodeColor() => new(0.52f, 0.62f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => Mathf.Clamp(Outputs, 1, 8);
        public override string GetOutputPortName(int port) => $"Then {port}";
        public override bool CanBePrime() => false;

        public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
        {
            for (int i = 0; i < GetOutputCount(); i++)
                runtime.PropagateFromOutput(Id, i);
        }
        public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
        public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
        {
            completion = new NodeCompletion(-1);
            return true;
        }
        public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) { }
    }

    public class CharacterWaitEventNodeData : GraphNodeData, IFlowNode
    {
        public string EventName { get; set; } = string.Empty;

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Wait Event";
        public override string GetCategory() => "Character / Flow";
        public override Color GetNodeColor() => new(0.52f, 0.62f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 1;
        public override bool CanBePrime() => false;

        public void Enter(FlowGraphRuntime runtime, GraphExecutionContext context)
        {
            CharacterGraphRuntime graph = context.GetUserData<CharacterGraphRuntime>();
            runtime.SetNodeData(Id, new WaitData { Version = graph?.GetEventVersion(EventName) ?? 0 });
        }
        public void Tick(FlowGraphRuntime runtime, GraphExecutionContext context, double delta) { }
        public bool TryGetCompletion(FlowGraphRuntime runtime, GraphExecutionContext context, out NodeCompletion completion)
        {
            int version = context.GetUserData<CharacterGraphRuntime>()?.GetEventVersion(EventName) ?? 0;
            bool changed = runtime.TryGetNodeData<WaitData>(Id, out WaitData data) && version > data.Version;
            completion = changed ? NodeCompletion.Completed() : default;
            return changed;
        }
        public void Exit(FlowGraphRuntime runtime, GraphExecutionContext context) => runtime.ClearNodeData(Id);

        private sealed class WaitData { public int Version; }

        public override Control CreateInspectorUI(GraphEditorContext context)
        {
            var edit = new LineEdit { Text = EventName, PlaceholderText = "Ability.attack.Completed" };
            edit.TextChanged += value => EventName = value.Trim();
            return edit;
        }
    }

    public class CharacterAbilityNodeData : GraphNodeData, IFlowNode
    {
        public string AbilityId { get; set; } = string.Empty;
        public string AbilityResourcePath { get; set; } = string.Empty;

        public override List<string> GetGraphTypes() => new() { CharacterGraphAsset.CharacterGraphTypeName };
        public override string GetMenuName() => "Activate Ability";
        public override string GetCategory() => "Character / Abilities";
        public override string GetDisplayName() => string.IsNullOrWhiteSpace(AbilityId) ? "Ability" : AbilityId;
        public override Color GetNodeColor() => new(0.75f, 0.45f, 0.9f);
        public override int GetInputCount() => 1;
        public override int GetOutputCount() => 4;
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
                data.Runtime = context.GetUserData<GameObject2D>()?
                    .GetComponent<AbilitySystemComponent2D>()?
                    .GetRuntime(AbilityId);
                runtime.PropagateFromOutput(Id, 0);
            }
            runtime.SetNodeData(Id, data);
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
            if (data.Runtime?.IsRunning == true)
            {
                completion = default;
                return false;
            }
            bool cancelled = string.Equals(data.Runtime?.LastReturnLabel, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(data.Runtime?.LastReturnLabel, "Interrupted", StringComparison.OrdinalIgnoreCase);
            completion = new NodeCompletion(cancelled ? 2 : 1, data.Runtime?.LastReturnLabel ?? string.Empty);
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
                if (ability?.Graph != null)
                    EditorInterface.Singleton.EditResource(ability.Graph);
            };
            root.AddChild(open);
#endif
            return root;
        }

        private sealed class AbilityNodeRuntimeData
        {
            public AbilityActivationResult Result;
            public AbilityRuntime Runtime;
        }
    }
}
