using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic
{
    public sealed class CharacterGraphRuntime
    {
        private readonly CharacterGraphAsset _graph;
        private readonly GameObject2D _owner;
        private readonly ICharacterInputProvider _input;
        private readonly AbilitySystemComponent2D _abilities;
        private readonly GraphBlackboardRuntime _blackboard = new();
        private readonly List<EventExecution> _executions = new();
        private readonly Dictionary<string, int> _eventVersions = new(StringComparer.Ordinal);
        private bool _began;
        private bool _stopped;

        public CharacterGraphRuntime(
            CharacterGraphAsset graph,
            GameObject2D owner,
            ICharacterInputProvider input)
        {
            _graph = graph;
            _owner = owner;
            _input = input;
            _abilities = owner?.GetComponent<AbilitySystemComponent2D>();
            _blackboard.PushLocal(graph);
            if (_abilities != null)
                _abilities.AbilityCompleted += OnAbilityCompleted;
        }

        public CharacterGraphAsset Graph => _graph;
        public GameObject2D Owner => _owner;
        public bool IsRunning => !_stopped;
        public int ActiveExecutionCount => _executions.Count;

        public void Update(double delta, bool physics)
        {
            if (_stopped)
                return;

            TickExecutions(delta, physics);
            if (!_began)
            {
                _began = true;
                TriggerLifecycle(CharacterLifecycleEvent.BeginPlay, physics);
            }

            TriggerLifecycle(
                physics ? CharacterLifecycleEvent.PhysicsUpdate : CharacterLifecycleEvent.Update,
                physics);
            if (physics)
                PollInputEvents();
        }

        public void Stop()
        {
            if (_stopped)
                return;
            TriggerLifecycle(CharacterLifecycleEvent.EndPlay, physics: false);
            for (int i = _executions.Count - 1; i >= 0; i--)
                _executions[i].Runtime.Stop();
            _executions.Clear();
            if (_abilities != null)
                _abilities.AbilityCompleted -= OnAbilityCompleted;
            _blackboard.PopLocal();
            _stopped = true;
        }

        public AbilityActivationResult TryActivateAbility(CharacterAbilityNodeData node)
        {
            if (_abilities == null || node == null)
                return AbilityActivationResult.InvalidContext;

            AbilityRuntime current = _abilities.ActiveAbilities.FirstOrDefault(value => value?.IsRunning == true);
            CharacterGraphConnection relation = null;
            if (current != null && !string.Equals(current.AbilityId, node.AbilityId, StringComparison.Ordinal))
            {
                relation = FindRelation(
                    current.AbilityId,
                    node.Id,
                    CharacterGraphRelationKind.Interrupt);
                if (!CanUseRelation(relation, current.ElapsedTime))
                    return AbilityActivationResult.BlockedByCurrentAbility;
            }

            AbilityActivationResult result = _abilities.TryActivateAbility(
                node.AbilityId,
                "CharacterGraph",
                relation?.RequestPriority);
            PublishEvent($"Ability.{node.AbilityId}.{result}");
            return result;
        }

        public void PublishEvent(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;
            _eventVersions.TryGetValue(eventName, out int version);
            _eventVersions[eventName] = version + 1;
        }

        public int GetEventVersion(string eventName) =>
            !string.IsNullOrWhiteSpace(eventName) && _eventVersions.TryGetValue(eventName, out int version)
                ? version
                : 0;

        private void TriggerLifecycle(CharacterLifecycleEvent eventType, bool physics)
        {
            foreach (CharacterLifecycleEventNodeData node in _graph.Nodes.OfType<CharacterLifecycleEventNodeData>())
            {
                if (node.Event == eventType)
                    Trigger(node, physics, null);
            }
            PublishEvent($"Lifecycle.{eventType}");
        }

        private void PollInputEvents()
        {
            if (_input == null)
                return;
            foreach (CharacterInputActionNodeData node in _graph.Nodes.OfType<CharacterInputActionNodeData>())
            {
                if (!node.IsTriggered(_input))
                    continue;
                float value = node.ReadValue(_input);
                Trigger(node, physics: true, new CharacterInputEventContext { NodeId = node.Id, Value = value });
                PublishEvent($"Input.{node.Id}");
            }
        }

        private void Trigger(GraphNodeData eventNode, bool physics, CharacterInputEventContext inputEvent)
        {
            if (eventNode == null || _executions.Any(value => value.EventNodeId == eventNode.Id))
                return;

            var context = new GraphExecutionContext(_graph, _blackboard);
            context.UserData.Add(this);
            context.UserData.Add(_owner);
            if (inputEvent != null)
                context.UserData.Add(inputEvent);

            var runtime = new CharacterEventFlowRuntime(_graph, context)
            {
                ManageLocalBlackboardScope = false
            };
            var execution = new EventExecution(eventNode.Id, physics, runtime);
            _executions.Add(execution);
            if (!runtime.StartFromNode(eventNode) || runtime.IsCompleted)
            {
                runtime.Stop();
                _executions.Remove(execution);
            }
        }

        private void TickExecutions(double delta, bool physics)
        {
            for (int i = _executions.Count - 1; i >= 0; i--)
            {
                EventExecution execution = _executions[i];
                if (execution.Physics != physics)
                    continue;
                execution.Runtime.Update(delta);
                if (!execution.Runtime.IsCompleted)
                    continue;
                execution.Runtime.Stop();
                _executions.RemoveAt(i);
            }
        }

        private CharacterGraphConnection FindRelation(
            string sourceAbilityId,
            string targetNodeId,
            CharacterGraphRelationKind kind)
        {
            foreach (CharacterAbilityNodeData source in _graph.Nodes.OfType<CharacterAbilityNodeData>())
            {
                if (!string.Equals(source.AbilityId, sourceAbilityId, StringComparison.Ordinal))
                    continue;
                CharacterGraphConnection relation = _graph.Connections
                    .OfType<CharacterGraphConnection>()
                    .FirstOrDefault(value => value.FromNode == source.Id &&
                        value.ToNode == targetNodeId && value.RelationKind == kind);
                if (relation != null)
                    return relation;
            }
            return null;
        }

        private void OnAbilityCompleted(AbilityRuntime runtime)
        {
            if (runtime == null)
                return;
            PublishEvent($"Ability.{runtime.AbilityId}.Completed");
            foreach (CharacterAbilityNodeData source in _graph.Nodes.OfType<CharacterAbilityNodeData>())
            {
                if (!string.Equals(source.AbilityId, runtime.AbilityId, StringComparison.Ordinal))
                    continue;
                foreach (CharacterGraphConnection relation in _graph.Connections.OfType<CharacterGraphConnection>())
                {
                    if (relation.FromNode != source.Id || relation.RelationKind != CharacterGraphRelationKind.Completion)
                        continue;
                    if (!CanUseRelation(relation, runtime.ElapsedTime))
                        continue;
                    if (_graph.FindNodeById(relation.ToNode) is CharacterAbilityNodeData target)
                        _abilities.TryActivateAbility(
                            target.AbilityId,
                            "CharacterGraph.Completion",
                            relation.RequestPriority);
                }
            }
        }

        private bool CanUseRelation(CharacterGraphConnection relation, double elapsed)
        {
            if (relation == null || !relation.IsWithinWindow(elapsed))
                return false;

            var context = new GraphExecutionContext(_graph, _blackboard);
            context.UserData.Add(this);
            if (_owner != null)
                context.UserData.Add(_owner);
            return relation.CanTraverse(context);
        }

        private sealed class CharacterEventFlowRuntime : FlowGraphRuntime
        {
            public CharacterEventFlowRuntime(CharacterGraphAsset graph, GraphExecutionContext context)
                : base(graph, context) { }

            protected override bool ShouldTraverseConnection(GraphConnection connection, FlowConnectionMode mode)
            {
                if (connection is CharacterGraphConnection character &&
                    character.RelationKind != CharacterGraphRelationKind.Flow)
                    return false;
                return base.ShouldTraverseConnection(connection, mode);
            }
        }

        private sealed class EventExecution
        {
            public EventExecution(string eventNodeId, bool physics, FlowGraphRuntime runtime)
            {
                EventNodeId = eventNodeId;
                Physics = physics;
                Runtime = runtime;
            }
            public string EventNodeId { get; }
            public bool Physics { get; }
            public FlowGraphRuntime Runtime { get; }
        }
    }
}
