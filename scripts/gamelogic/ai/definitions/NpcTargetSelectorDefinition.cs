using Godot;
namespace GameLogic.AI;

public enum NpcTargetSource { PerceivedPoints, WorldPoints, IdentityParameter, CurrentLocation, KnownPoints, FixedObject }
public enum NpcTargetSelection { Candidates, Nearest, LowestScore, HighestScore }

/// <summary>A reusable query. Each planning run freezes its candidates; resources contain no live bindings.</summary>
[Tool, GlobalClass]
public partial class NpcTargetSelectorDefinition : NpcDefinitionResource
{
    [Export] public string Adapter { get; set; } = "point";
    NpcTargetSource _source;
    [Export] public NpcTargetSource Source { get=>_source; set {if(_source==value)return;_source=value;NotifyPropertyListChanged();} }
    [Export] public string ParameterKey { get; set; } = "";
    [Export] public string FixedObjectId { get; set; } = "";
    [Export] public string[] AllTags { get; set; } = System.Array.Empty<string>();
    [Export] public string[] AnyTags { get; set; } = System.Array.Empty<string>();
    [Export] public string[] ExcludedTags { get; set; } = System.Array.Empty<string>();
    [Export] public double MaximumDistance { get; set; }
    [Export] public bool RequireUsable { get; set; } = true;
    [Export] public bool RequireReachable { get; set; } = true;
    [Export] public CharacterExpressionDefinition Filter { get; set; }
    NpcTargetSelection _selection;
    [Export] public NpcTargetSelection Selection { get=>_selection; set {if(_selection==value)return;_selection=value;NotifyPropertyListChanged();} }
    [Export] public CharacterExpressionDefinition Score { get; set; }
    [Export] public int CandidateLimit { get; set; }
    public override string Category => "目标选择器";
    public static bool IsSingleSource(NpcTargetSource source)=>source is NpcTargetSource.FixedObject or NpcTargetSource.IdentityParameter or NpcTargetSource.CurrentLocation;
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        base._ValidateProperty(property);
        string name=property["name"].AsString();
        bool single=IsSingleSource(Source);
        if(name==nameof(ParameterKey)&&Source!=NpcTargetSource.IdentityParameter ||
            name==nameof(FixedObjectId)&&Source!=NpcTargetSource.FixedObject ||
            name==nameof(Selection)&&single ||
            name==nameof(Score)&&(single||Selection is not (NpcTargetSelection.LowestScore or NpcTargetSelection.HighestScore)) ||
            name==nameof(CandidateLimit)&&(single||Selection!=NpcTargetSelection.Candidates))
            property["usage"]=(long)PropertyUsageFlags.Storage;
    }
}
