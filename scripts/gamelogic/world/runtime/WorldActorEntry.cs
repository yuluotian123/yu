using Godot;
using System.Linq;
using GameLogic.AI;
namespace GameLogic;

public sealed record WorldActorEntry(string Id,string ScenePath,string LevelId,Vector3 Position,CharacterProfile Profile,NpcGraphAsset Graph,
    float Speed,float DetailedDistance=30,float AbstractDistance=45,double ResidencySeconds=2)
{
    public Godot.Collections.Array<CharacterScalarDefinition> ParameterOverrides {get;init;}=new();
    public float HalfHeight {get;init;}=.36f;
    public bool Running {get;init;}=true;
    public Godot.Collections.Dictionary Encode()=>new(){{"id",Id},{"scene",ScenePath},{"level",LevelId},{"position",Position},
        {"profile",Profile},{"graph",Graph},{"speed",Speed},{"detailed",DetailedDistance},{"abstract",AbstractDistance},{"residency",ResidencySeconds},{"overrides",ParameterOverrides},{"half_height",HalfHeight},{"running",Running}};
    public static WorldActorEntry Decode(Godot.Collections.Dictionary d)=>new(d["id"].AsString(),d["scene"].AsString(),d["level"].AsString(),d["position"].AsVector3(),
        d["profile"].As<CharacterProfile>(),d["graph"].As<NpcGraphAsset>(),d["speed"].AsSingle(),d["detailed"].AsSingle(),d["abstract"].AsSingle(),d["residency"].AsDouble())
        {ParameterOverrides=d.TryGetValue("overrides",out var overrides)?new(overrides.AsGodotArray().Select(v=>v.As<CharacterScalarDefinition>())):new(),HalfHeight=d.TryGetValue("half_height",out var height)?height.AsSingle():.36f,Running=!d.TryGetValue("running",out var running)||running.AsBool()};
}
