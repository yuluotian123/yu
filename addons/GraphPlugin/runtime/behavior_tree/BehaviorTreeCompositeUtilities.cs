using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BehaviorTreeSelectorUtility
{
    public static BehaviorTreeStatus TickSelector(
        BehaviorTreeNodeData node,
        BehaviorTreeRuntime runtime,
        double delta,
        bool memory,
        bool random,
        bool weighted)
    {
        List<BehaviorTreeNodeData> children = GetOrderedChildren(node, runtime, random, weighted);
        if (children.Count == 0)
            return BehaviorTreeStatus.Failure;

        BehaviorTreeCompositeRuntimeData data = runtime.GetNodeData<BehaviorTreeCompositeRuntimeData>(node.Id);
        int start = memory && data.RunningIndex >= 0 ? data.RunningIndex : 0;
        int previous = data.RunningIndex;

        for (int i = start; i < children.Count; i++)
        {
            BehaviorTreeStatus status = runtime.TickNode(children[i], delta);
            if (status == BehaviorTreeStatus.Running)
            {
                AbortPreviousIfChanged(runtime, children, previous, i);
                data.RunningIndex = i;
                return BehaviorTreeStatus.Running;
            }

            if (status == BehaviorTreeStatus.Success)
            {
                AbortPreviousIfChanged(runtime, children, previous, i);
                runtime.ClearNodeData(node.Id);
                return BehaviorTreeStatus.Success;
            }
        }

        AbortPreviousIfChanged(runtime, children, previous, -1);
        runtime.ClearNodeData(node.Id);
        return BehaviorTreeStatus.Failure;
    }

    private static void AbortPreviousIfChanged(
        BehaviorTreeRuntime runtime,
        List<BehaviorTreeNodeData> children,
        int previous,
        int current)
    {
        if (previous >= 0 && previous != current && previous < children.Count)
            runtime.AbortSubtree(children[previous]);
    }

    private static List<BehaviorTreeNodeData> GetOrderedChildren(
        BehaviorTreeNodeData node,
        BehaviorTreeRuntime runtime,
        bool random,
        bool weighted)
    {
        if (!random && !weighted)
            return runtime.GetChildren(node);

        BehaviorTreeCompositeRuntimeData data = runtime.GetNodeData<BehaviorTreeCompositeRuntimeData>(node.Id);
        if (data.OrderedNodeIds == null || data.OrderedNodeIds.Count == 0)
        {
            List<BehaviorTreeChildLink> links = runtime.GetChildLinks(node);
            data.OrderedNodeIds = weighted
                ? BuildWeightedOrder(links, runtime)
                : BuildRandomOrder(links, runtime);
        }

        return data.OrderedNodeIds
            .Select(id => runtime.Graph.FindNodeById(id))
            .OfType<BehaviorTreeNodeData>()
            .ToList();
    }

    private static List<string> BuildRandomOrder(List<BehaviorTreeChildLink> links, BehaviorTreeRuntime runtime)
    {
        var result = links.Select(link => link.Node.Id).ToList();
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = Mathf.FloorToInt(runtime.Randf() * (i + 1));
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    private static List<string> BuildWeightedOrder(List<BehaviorTreeChildLink> links, BehaviorTreeRuntime runtime)
    {
        var remaining = new List<BehaviorTreeChildLink>(links);
        var result = new List<string>();

        while (remaining.Count > 0)
        {
            float total = remaining.Sum(link => Mathf.Max(0.001f, link.Connection.Weight));
            float roll = runtime.Randf() * total;
            int selected = 0;

            for (int i = 0; i < remaining.Count; i++)
            {
                roll -= Mathf.Max(0.001f, remaining[i].Connection.Weight);
                if (roll <= 0f)
                {
                    selected = i;
                    break;
                }
            }

            result.Add(remaining[selected].Node.Id);
            remaining.RemoveAt(selected);
        }

        return result;
    }
}

internal static class BehaviorTreeSequenceUtility
{
    public static BehaviorTreeStatus TickSequence(
        BehaviorTreeNodeData node,
        BehaviorTreeRuntime runtime,
        double delta,
        bool memory)
    {
        List<BehaviorTreeNodeData> children = runtime.GetChildren(node);
        if (children.Count == 0)
            return BehaviorTreeStatus.Success;

        BehaviorTreeCompositeRuntimeData data = runtime.GetNodeData<BehaviorTreeCompositeRuntimeData>(node.Id);
        int start = memory && data.RunningIndex >= 0 ? data.RunningIndex : 0;
        int previous = data.RunningIndex;

        for (int i = start; i < children.Count; i++)
        {
            BehaviorTreeStatus status = runtime.TickNode(children[i], delta);
            if (status == BehaviorTreeStatus.Running)
            {
                if (previous >= 0 && previous != i)
                    runtime.AbortChildAt(node, previous);

                data.RunningIndex = i;
                return BehaviorTreeStatus.Running;
            }

            if (status == BehaviorTreeStatus.Failure)
            {
                if (previous >= 0 && previous != i)
                    runtime.AbortChildAt(node, previous);

                runtime.ClearNodeData(node.Id);
                return BehaviorTreeStatus.Failure;
            }
        }

        runtime.ClearNodeData(node.Id);
        return BehaviorTreeStatus.Success;
    }
}
