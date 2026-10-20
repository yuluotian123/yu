using Godot;
namespace GameLogic.AI;

/// <summary>Stable action input identity and per-action resource requirements.</summary>
[Tool, GlobalClass]
public partial class NpcActionInputDefinition : Resource
{
    [Export] public string Name { get; set; } = "参数";
    string _inputType="number";
    [Export] public string InputType { get=>_inputType;set{_inputType=value;NotifyPropertyListChanged();} }
    [Export] public CharacterExpressionDefinition Value { get; set; }
    [Export] public string InputId { get; set; } = System.Guid.NewGuid().ToString("N");
    [Export] public string[] ResourceRequirements { get; set; } = System.Array.Empty<string>();
    public bool IsTarget=>InputType.StartsWith("target:",System.StringComparison.Ordinal);
    public string Adapter=>IsTarget?InputType[7..]:"";
    public CharacterValueType ValueType=>IsTarget?CharacterValueType.Symbol:InputType switch
    {"number"=>CharacterValueType.Number,"boolean"=>CharacterValueType.Boolean,"text"=>CharacterValueType.Symbol,_=>CharacterValueType.Unknown};
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        string name=property["name"].AsString();
        if(name==nameof(InputId))property["usage"]=(long)PropertyUsageFlags.Storage;
        if(name==nameof(Value)&&IsTarget)property["usage"]=(long)PropertyUsageFlags.None;
        if(name==nameof(ResourceRequirements)&&!IsTarget)property["usage"]=(long)PropertyUsageFlags.None;
        if(property["name"].AsString()==nameof(ResourceRequirements)&&
            IsTarget&&NpcBindingAdapters.Find(Adapter)?.Capabilities.Count is not >0)
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
