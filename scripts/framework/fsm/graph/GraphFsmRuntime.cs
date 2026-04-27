using System;
using System.Collections.Generic;
using System.Linq;

namespace Framework
{
    public sealed class GraphFsmRuntime
    {
        private readonly HashSet<string> _triggers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _boolParameters = new(StringComparer.Ordinal);

        public GraphFsmRuntime(GraphFsmGraphAsset graph)
        {
            Graph = graph;
        }

        public GraphFsmGraphAsset Graph { get; }
        public GraphFsmStateNodeData CurrentState { get; private set; }
        public string CurrentStateName => CurrentState?.StateName ?? string.Empty;
        public double CurrentStateTime { get; private set; }
        public bool IsRunning => CurrentState != null;

        public event Action<GraphFsmStateNodeData, GraphFsmStateNodeData, GraphFsmTransitionConnection> StateChanged;

        public bool Start(string initialStateName = null)
        {
            if (Graph == null)
                return false;

            GraphFsmStateNodeData initialState = Graph.GetInitialState(initialStateName);
            if (initialState == null)
                return false;

            CurrentState = initialState;
            CurrentStateTime = 0d;
            return true;
        }

        public void Process(double delta)
        {
            if (!IsRunning)
                return;

            CurrentStateTime += delta;

            try
            {
                GraphFsmTransitionConnection transition = Graph
                    .GetOutgoingTransitions(CurrentState.Id)
                    .FirstOrDefault(CanUseTransition);

                if (transition != null)
                    ChangeStateById(transition.ToNode, transition);
            }
            finally
            {
                _triggers.Clear();
            }
        }

        public void Trigger(string triggerName)
        {
            if (!string.IsNullOrWhiteSpace(triggerName))
                _triggers.Add(triggerName);
        }

        public void SetBool(string parameterName, bool value)
        {
            if (!string.IsNullOrWhiteSpace(parameterName))
                _boolParameters[parameterName] = value;
        }

        public bool GetBool(string parameterName)
        {
            return !string.IsNullOrWhiteSpace(parameterName) &&
                   _boolParameters.TryGetValue(parameterName, out bool value) &&
                   value;
        }

        public bool ChangeState(string stateName)
        {
            if (Graph == null)
                return false;

            GraphFsmStateNodeData state = Graph.FindStateByName(stateName) ?? Graph.FindStateById(stateName);
            return state != null && ChangeState(state, null);
        }

        private bool ChangeStateById(string stateId, GraphFsmTransitionConnection transition)
        {
            GraphFsmStateNodeData state = Graph.FindStateById(stateId);
            return state != null && ChangeState(state, transition);
        }

        private bool ChangeState(GraphFsmStateNodeData nextState, GraphFsmTransitionConnection transition)
        {
            if (nextState == null || nextState == CurrentState)
                return false;

            GraphFsmStateNodeData previousState = CurrentState;
            CurrentState = nextState;
            CurrentStateTime = 0d;
            StateChanged?.Invoke(previousState, CurrentState, transition);
            return true;
        }

        private bool CanUseTransition(GraphFsmTransitionConnection transition)
        {
            if (transition == null)
                return false;

            return transition.Condition switch
            {
                GraphFsmTransitionCondition.Always => true,
                GraphFsmTransitionCondition.Trigger => !string.IsNullOrWhiteSpace(transition.TriggerName) &&
                                                       _triggers.Contains(transition.TriggerName),
                GraphFsmTransitionCondition.BoolEquals => !string.IsNullOrWhiteSpace(transition.BoolParameterName) &&
                                                          _boolParameters.TryGetValue(transition.BoolParameterName, out bool value) &&
                                                          value == transition.ExpectedBoolValue,
                GraphFsmTransitionCondition.Timer => CurrentStateTime >= transition.DelaySeconds,
                _ => false
            };
        }
    }
}
