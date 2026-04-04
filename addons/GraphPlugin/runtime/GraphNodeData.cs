using System.Collections.Generic;
using Godot;


/// <summary>
/// 图节点数据，纯 C# 类。
/// 序列化由 GraphJsonHelper 负责（存储在 GraphAsset.NodesJson 中）。
/// </summary>
public class GraphNodeData
{
    public string Id { get; set; } = "";
    public Vector2 Position { get; set; } = Vector2.Zero;
    public string GraphName { get; set; } = "";

    public GraphNodeData()
    {
        if (string.IsNullOrEmpty(Id))
            Id = GenerateUniqueId();
    }

    private static string GenerateUniqueId()
    {
        var time = Time.GetTicksUsec();
        var rand1 = (uint)GD.Randi();
        var rand2 = (uint)GD.Randi();
        return $"{time:x}_{rand1:x}_{rand2:x}";
    }


    private string? _nodeType;

    public virtual string NodeType
    {
        get => _nodeType ?? GetType().Name;
        set => _nodeType = value;
    }
    public virtual List<string> GetGraphTypes()
        => new List<string> { "All" };

    public virtual string GetDisplayName() => NodeType;
    public virtual Color GetNodeColor() => Colors.White;
    public virtual int GetInputCount() => 0;
    public virtual int GetOutputCount() => 0;
    public virtual bool CanBePrime()=> true;
    /// <summary>
    /// 返回指定输出端口允许的最大连线数量。-1 表示不限制（默认）。
    /// </summary>
    public virtual int GetOutputMaxConnections(int port) => -1;
    /// <summary>
    /// 返回指定输入端口允许的最大连线数量。默认 1（通常输入只接受一条线）。
    /// </summary>
    public virtual int GetInputMaxConnections(int port) => 1;

    public virtual void CreateUI(GraphNode node)
    {
        var label = new Label { Text = GetDisplayName() };
        node.AddChild(label);
    }
    
    /// <summary>
    /// 处理瞬时逻辑，在到达节点时调用；并不代表会退出此节点
    /// </summary>
    public virtual void Execute() { }

}
