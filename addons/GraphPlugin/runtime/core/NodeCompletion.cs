public readonly struct NodeCompletion
{
    public const int CompletedPort = 0;
    public const int NextPort = 0;
    public const int TruePort = 0;
    public const int FalsePort = 1;
    public const int ReturnPort = 0;

    public NodeCompletion(int outputPort, string label = "")
    {
        OutputPort = outputPort;
        Label = label ?? string.Empty;
    }

    public int OutputPort { get; }
    public string Label { get; }

    public static NodeCompletion Completed(string label = "Completed") => new(CompletedPort, label);
    public static NodeCompletion Next(string label = "Next") => new(NextPort, label);
    public static NodeCompletion True(string label = "True") => new(TruePort, label);
    public static NodeCompletion False(string label = "False") => new(FalsePort, label);
    public static NodeCompletion Return(string label = "") => new(ReturnPort, label);
}
