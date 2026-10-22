using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic;

/// <summary>Generated directory data. Scene components are the only authoring source.</summary>
public sealed record WorldCatalogEntry(string Id,string DisplayName,WorldObjectTypeDefinition Type,WorldLevelDefinition Level,
    WorldLocationDefinition Location,string[] Tags,string OwnerId,IReadOnlyList<WorldCapabilityDefinition> Capabilities)
{
    public Vector3? Position {get;init;}
    public IEnumerable<WorldCapabilityDefinition> EffectiveCapabilities=>Capabilities;
    public string[] AllTags=>(Type?.Tags??System.Array.Empty<string>()).Concat(Tags).Distinct().ToArray();
    public T Capability<T>() where T:WorldCapabilityDefinition=>Capabilities.OfType<T>().SingleOrDefault();
    public Godot.Collections.Dictionary Encode()
    {
        using var capabilities=(Godot.Collections.Array)new Godot.Collections.Array<WorldCapabilityDefinition>(Capabilities);
        var data=new Godot.Collections.Dictionary{{"id",Id},{"name",DisplayName},{"type",Type},{"location",Location},{"tags",Tags},{"owner",OwnerId},{"capabilities",capabilities}};
        if(Position.HasValue)data["position"]=Position.Value;return data;
    }
    public static WorldCatalogEntry Decode(Godot.Collections.Dictionary data,WorldLevelDefinition level)
    {
        using var capabilities=data["capabilities"].AsGodotArray();
        return new(data["id"].AsString(),data["name"].AsString(),data["type"].As<WorldObjectTypeDefinition>(),level,
            data["location"].As<WorldLocationDefinition>(),data["tags"].AsStringArray(),data["owner"].AsString(),
            System.Array.AsReadOnly(capabilities.Select(v=>v.As<WorldCapabilityDefinition>()).ToArray())){Position=data.ContainsKey("position")?data["position"].AsVector3():null};
    }
}
