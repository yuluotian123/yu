using System;
using System.Collections.Generic;
using Framework;
using Godot;

namespace GameLogic
{
    [GlobalClass]
    public partial class CharacterGraphComponent2D : HfsmComponent2D
    {
        [Export] public CharacterGraphAsset CharacterGraph { get; set; }
        [Export] public bool PhysicalInputEnabled { get; set; } = true;

        private readonly Dictionary<HfsmRuntime, string> _resumeStateIds = new();
        private readonly Dictionary<CharacterGraphAsset, List<CompiledInput>> _compiledInputs = new();
        private readonly List<RuntimeScope> _activeScopes = new();
        private readonly List<HfsmRuntime> _staleRuntimes = new();
        private CharacterCommandBufferComponent2D _commands;
        private InputModuleCharacterInputProvider _physicalInput;

        public override int Priority => ComponentPriority.State;

        protected override HfsmGraphAsset ResolveGraph()
        {
            return CharacterGraph ?? base.ResolveGraph();
        }

        public override void OnInit()
        {
            if (CharacterGraph != null)
                Graph = CharacterGraph;

            _commands = Owner?.GetComponent<CharacterCommandBufferComponent2D>();
            _physicalInput = new InputModuleCharacterInputProvider(ModuleSystem.GetModule<IInputModule>());
            base.OnInit();
        }

        public override void OnPhysicsUpdate(double delta)
        {
            CharacterActionRequest request = _commands?.ConsumeAction() ?? default;
            PublishCommandSnapshot(_commands?.Pending ?? CharacterCommand2D.None);

            CollectActiveScopes();
            TryActivateInput(request);
            base.OnPhysicsUpdate(delta);

            CollectActiveScopes();
            ResumeCompletedActions();
            PublishActiveAction();
        }

        public override void OnDestroy()
        {
            _resumeStateIds.Clear();
            _compiledInputs.Clear();
            _activeScopes.Clear();
            _staleRuntimes.Clear();
            _commands = null;
            _physicalInput = null;
            base.OnDestroy();
        }

        public void SubmitAction(CharacterActionRequest request)
        {
            _commands?.SubmitAction(request);
        }

        private void TryActivateInput(CharacterActionRequest request)
        {
            bool hasSelected = false;
            ActionCandidate selected = default;
            ICharacterInputProvider physicalInput = PhysicalInputEnabled && _physicalInput?.IsAvailable == true
                ? _physicalInput
                : null;

            for (int scopeIndex = 0; scopeIndex < _activeScopes.Count; scopeIndex++)
            {
                RuntimeScope scope = _activeScopes[scopeIndex];
                if (scope.Runtime.Graph is not CharacterGraphAsset graph)
                    continue;

                List<CompiledInput> inputs = GetCompiledInputs(graph);
                for (int inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
                {
                    CompiledInput compiled = inputs[inputIndex];
                    bool fromRequest = compiled.Input.MatchesRequest(request);
                    bool fromPhysicalInput = compiled.Input.IsTriggered(physicalInput);
                    if (!fromRequest && !fromPhysicalInput)
                        continue;

                    int requestPriority = fromRequest ? request.Priority : int.MinValue;
                    for (int routeIndex = 0; routeIndex < compiled.Routes.Count; routeIndex++)
                    {
                        CompiledRoute route = compiled.Routes[routeIndex];
                        if (!route.Transition.CanUse(scope.Runtime) ||
                            route.Action == scope.Runtime.CurrentState ||
                            !route.Action.CanEnter(scope.Runtime))
                        {
                            continue;
                        }

                        var candidate = new ActionCandidate(
                            scope,
                            compiled.Input,
                            route.Action,
                            route.Transition,
                            fromPhysicalInput,
                            requestPriority);
                        if (!hasSelected || IsBetterCandidate(candidate, selected))
                        {
                            selected = candidate;
                            hasSelected = true;
                        }
                    }
                }
            }

            if (!hasSelected)
                return;

            HfsmRuntime runtime = selected.Scope.Runtime;
            if (runtime.CurrentState is not CharacterSkillChainNodeData &&
                !_resumeStateIds.ContainsKey(runtime))
            {
                _resumeStateIds[runtime] = runtime.CurrentStateId;
            }

            if (runtime.TryTransitionTo(selected.Action.Id, selected.Transition))
            {
                selected.Input.Accept(
                    runtime,
                    physicalInput,
                    selected.FromPhysicalInput);
            }
        }

        private void ResumeCompletedActions()
        {
            _staleRuntimes.Clear();
            foreach (HfsmRuntime runtime in _resumeStateIds.Keys)
            {
                if (!ContainsActiveRuntime(runtime))
                    _staleRuntimes.Add(runtime);
            }

            for (int i = 0; i < _staleRuntimes.Count; i++)
                _resumeStateIds.Remove(_staleRuntimes[i]);

            for (int scopeIndex = _activeScopes.Count - 1; scopeIndex >= 0; scopeIndex--)
            {
                HfsmRuntime runtime = _activeScopes[scopeIndex].Runtime;
                if (runtime.CurrentState is not CharacterSkillChainNodeData action)
                {
                    _resumeStateIds.Remove(runtime);
                    continue;
                }

                if (!action.TryGetCompletion(runtime, out _))
                    continue;

                _resumeStateIds.TryGetValue(runtime, out string resumeStateId);
                IStateNodeData fallback = runtime.Graph.GetInitialState();
                bool resumed = !string.IsNullOrWhiteSpace(resumeStateId) && runtime.TryTransitionTo(resumeStateId);
                if (!resumed && fallback != null)
                    resumed = runtime.TryTransitionTo(fallback.Id);

                if (resumed)
                    _resumeStateIds.Remove(runtime);
            }
        }

        private void CollectActiveScopes()
        {
            _activeScopes.Clear();
            HfsmRuntime runtime = Runtime;
            int depth = 0;
            while (runtime != null)
            {
                _activeScopes.Add(new RuntimeScope(runtime, depth));
                runtime = runtime.ChildRuntime;
                depth++;
            }
        }

        private bool ContainsActiveRuntime(HfsmRuntime runtime)
        {
            for (int i = 0; i < _activeScopes.Count; i++)
            {
                if (ReferenceEquals(_activeScopes[i].Runtime, runtime))
                    return true;
            }

            return false;
        }

        private List<CompiledInput> GetCompiledInputs(CharacterGraphAsset graph)
        {
            if (_compiledInputs.TryGetValue(graph, out List<CompiledInput> cached))
                return cached;

            var result = new List<CompiledInput>();
            for (int nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
            {
                if (graph.Nodes[nodeIndex] is not CharacterInputActionNodeData input)
                    continue;

                var compiled = new CompiledInput(input);
                foreach (StateTransitionConnection connection in graph.GetOutgoingTransitions(input.Id))
                {
                    if (connection is not HfsmTransitionConnection transition ||
                        graph.FindStateById(transition.ToNode) is not CharacterSkillChainNodeData action)
                    {
                        continue;
                    }

                    compiled.Routes.Add(new CompiledRoute(action, transition));
                }

                result.Add(compiled);
            }

            _compiledInputs[graph] = result;
            return result;
        }

        private void PublishCommandSnapshot(CharacterCommand2D command)
        {
            Runtime?.SetValue(CharacterGraphBlackboardKeys.CommandMoveAxisX, command.MoveAxisX);
            Runtime?.SetValue(CharacterGraphBlackboardKeys.CommandJumpStartRequested, command.JumpStartRequested);
            Runtime?.SetValue(CharacterGraphBlackboardKeys.CommandJumpSustainRequested, command.JumpSustainRequested);
        }

        private void PublishActiveAction()
        {
            string actionId = string.Empty;
            for (int i = _activeScopes.Count - 1; i >= 0; i--)
            {
                if (_activeScopes[i].Runtime.CurrentState is CharacterSkillChainNodeData action)
                {
                    actionId = action.ActionId;
                    break;
                }
            }

            Runtime?.SetValue(CharacterGraphBlackboardKeys.ActiveActionId, actionId);
        }

        private static bool IsBetterCandidate(ActionCandidate candidate, ActionCandidate current)
        {
            int comparison = candidate.Action.Priority.CompareTo(current.Action.Priority);
            if (comparison != 0)
                return comparison > 0;

            comparison = candidate.RequestPriority.CompareTo(current.RequestPriority);
            if (comparison != 0)
                return comparison > 0;

            comparison = candidate.Transition.Priority.CompareTo(current.Transition.Priority);
            if (comparison != 0)
                return comparison > 0;

            comparison = candidate.Scope.Depth.CompareTo(current.Scope.Depth);
            if (comparison != 0)
                return comparison > 0;

            return string.CompareOrdinal(candidate.Action.Id, current.Action.Id) < 0;
        }

        private readonly struct RuntimeScope
        {
            public RuntimeScope(HfsmRuntime runtime, int depth)
            {
                Runtime = runtime;
                Depth = depth;
            }

            public HfsmRuntime Runtime { get; }
            public int Depth { get; }
        }

        private sealed class CompiledInput
        {
            public CompiledInput(CharacterInputActionNodeData input)
            {
                Input = input;
            }

            public CharacterInputActionNodeData Input { get; }
            public List<CompiledRoute> Routes { get; } = new();
        }

        private readonly struct CompiledRoute
        {
            public CompiledRoute(
                CharacterSkillChainNodeData action,
                HfsmTransitionConnection transition)
            {
                Action = action;
                Transition = transition;
            }

            public CharacterSkillChainNodeData Action { get; }
            public HfsmTransitionConnection Transition { get; }
        }

        private readonly struct ActionCandidate
        {
            public ActionCandidate(
                RuntimeScope scope,
                CharacterInputActionNodeData input,
                CharacterSkillChainNodeData action,
                HfsmTransitionConnection transition,
                bool fromPhysicalInput,
                int requestPriority)
            {
                Scope = scope;
                Input = input;
                Action = action;
                Transition = transition;
                FromPhysicalInput = fromPhysicalInput;
                RequestPriority = requestPriority;
            }

            public RuntimeScope Scope { get; }
            public CharacterInputActionNodeData Input { get; }
            public CharacterSkillChainNodeData Action { get; }
            public HfsmTransitionConnection Transition { get; }
            public bool FromPhysicalInput { get; }
            public int RequestPriority { get; }
        }
    }
}
