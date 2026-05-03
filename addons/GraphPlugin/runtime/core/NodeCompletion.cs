/// <summary>
/// Flow 节点的完成结果。
/// 
/// OutputPort 决定完成后从哪个 Sequence 输出口继续推进。
/// Label 主要用于 debug、return 节点和日志展示。
/// </summary>
public readonly struct NodeCompletion
{
    /// <summary>
    /// 默认完成输出口。
    /// </summary>
    public const int CompletedPort = 0;
    public const int NextPort = 0;
    public const int TruePort = 0;
    public const int FalsePort = 1;
    public const int ReturnPort = 0;

    /// <summary>
    /// 特殊输出口：节点结束 active 状态，但不推进任何 Sequence 连接。
    /// Mission 取消或部署失败时会用它避免继续下游流程。
    /// </summary>
    public const int NoOutputPort = -1;

    public NodeCompletion(int outputPort, string label = "")
    {
        OutputPort = outputPort;
        Label = label ?? string.Empty;
    }

    public int OutputPort { get; }
    public string Label { get; }

    /// <summary>
    /// 普通完成，推进 0 号输出口。
    /// </summary>
    public static NodeCompletion Completed(string label = "Completed") => new(CompletedPort, label);

    /// <summary>
    /// “下一步”语义，等价于 Completed。
    /// </summary>
    public static NodeCompletion Next(string label = "Next") => new(NextPort, label);

    /// <summary>
    /// 条件成功，推进 0 号输出口。
    /// </summary>
    public static NodeCompletion True(string label = "True") => new(TruePort, label);

    /// <summary>
    /// 条件失败，推进 1 号输出口。
    /// </summary>
    public static NodeCompletion False(string label = "False") => new(FalsePort, label);

    /// <summary>
    /// Return 节点使用的完成结果。
    /// </summary>
    public static NodeCompletion Return(string label = "") => new(ReturnPort, label);

    /// <summary>
    /// 完成但不输出，不推进 Sequence。
    /// </summary>
    public static NodeCompletion NoOutput(string label = "") => new(NoOutputPort, label);
}
