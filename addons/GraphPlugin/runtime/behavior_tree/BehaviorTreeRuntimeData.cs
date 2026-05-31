using System.Collections.Generic;

public sealed class BehaviorTreeCompositeRuntimeData
{
    public int RunningIndex { get; set; } = -1;
    public List<string> OrderedNodeIds { get; set; }
}

public sealed class BehaviorTreeWaitRuntimeData
{
    public double Elapsed { get; set; }
}

public sealed class BehaviorTreeCooldownRuntimeData
{
    public double Remaining { get; set; }
}
