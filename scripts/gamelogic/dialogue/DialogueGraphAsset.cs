using Godot;

namespace GameLogic;

[Tool, GlobalClass]
public partial class DialogueGraphAsset : GraphAsset
{
    public const string TypeName = "Dialogue";
    public override string GraphType { get => TypeName; set { } }
    [Export] public string Title { get; set; } = "对话";
    [Export(PropertyHint.Range, "1,120,1")] public float CharactersPerSecond { get; set; } = 32;
    [Export] public bool AllowCancel { get; set; } = true;
    public override string GetEditorTitle() => "对话图 · " + Title;
    public override GraphConnection CreateConnection() => new();
}
