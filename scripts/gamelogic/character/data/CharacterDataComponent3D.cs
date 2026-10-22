using System;
using System.Linq;
using System.Text.Json.Nodes;
using Framework;
using Godot;
namespace GameLogic;

public interface ICharacterEnvironment
{
    double TotalHours {get;}
    double Rain {get;}
    string LocationAt(Vector3 position,string previous);
}

/// <summary>Shared character data is updated and saved independently of its controller.</summary>
[Tool, GlobalClass]
public partial class CharacterDataComponent3D : Component3D, ISaveSection
{
    public const string EnvironmentGroup="character_environment";
    public override int Priority=>ComponentPriority.CharacterData;
    // Default-value queries must not construct the profile's resource graph during registration.
    public CharacterDataComponent3D(){Profile=new();}
    [Export] public CharacterProfile Profile {get;set;}
    [Export] public Godot.Collections.Array<CharacterScalarDefinition> ParameterOverrides {get;set;}=new();
    [Export] public bool Persist {get;set;}=true;
    public CharacterState State {get;private set;}
    public WorldKnowledge WorldKnowledge {get;private set;}
    public string SectionKey=>"character_data";
    public string EntryKey=>Owner.PersistentId;
    public int SchemaVersion=>1;
    public event Action Restored;
    ICharacterEnvironment Environment=>((WorldModule)ModuleSystem.GetModule<IWorldModule>()).EnvironmentFor(Owner);
    public override void OnInit()
    {
        var schema=CharacterProfileSpec.Compile(Profile);
        foreach(var parameter in ParameterOverrides)
        {
            CharacterProfileSpec.Require(parameter!=null&&schema.Parameters.TryGetValue(parameter.Key,out var old)&&old.Type==parameter.Value.Type,"角色实例参数不存在或类型不匹配。");
            schema.Parameters[parameter.Key]=parameter.Value;
        }
        State=new(schema);
        WorldKnowledge=new(Profile.World);
        if(Persist)ModuleSystem.GetModule<ISaveModule>()?.RegisterSection(this);
    }
    public CharacterValue Parameter(string key)=>State?.Profile.Parameter(key)??default;
    public CharacterValue Read(string key)
    {
        if(key=="self.level")return CharacterValue.Text(((WorldModule)ModuleSystem.GetModule<IWorldModule>()).LevelFor(Owner)?.Id??"");
        if(key=="self.place")
        {
            var world=(WorldModule)ModuleSystem.GetModule<IWorldModule>();
            return CharacterValue.Text(world.ResolveObject(State?.Location)?.EffectiveLocation?.Id??world.LevelFor(Owner)?.Location?.Id??"");
        }
        if(key=="world.hour")return CharacterValue.Numeric((Environment?.TotalHours??ModuleSystem.GetModule<ITimeOfDayModule>()?.Clock.TotalHours??12)%24);
        if(key=="world.rain")
        {
            return CharacterValue.Numeric(Environment?.Rain??0);
        }
        return State?.Read(key)??default;
    }
    public override void OnUpdate(double delta)
    {
        if(State==null)return;
        var environment=Environment;
        State.Advance(environment?.TotalHours??ModuleSystem.GetModule<ITimeOfDayModule>()?.Clock.TotalHours??12,Read);
        if(environment!=null)State.UpdateLocation(environment.LocationAt(Owner.GlobalPosition,State.Location));
    }
    public JsonObject Capture(){var data=State?.Capture()??new();data["world_knowledge"]=WorldKnowledge?.Capture();return data;}
    public void Restore(JsonObject data,int version)
    {
        if(version!=SchemaVersion||State==null)return;
        State.Restore(data);WorldKnowledge.Restore(data["world_knowledge"] as JsonObject);Restored?.Invoke();
    }
    public override void OnDestroy(){if(Persist)ModuleSystem.GetModule<ISaveModule>()?.UnregisterSection(this);Restored=null;State=null;}
}
