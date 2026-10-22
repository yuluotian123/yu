using Godot;
using System.Linq;
namespace GameLogic;

[Tool, GlobalClass]
public partial class WorldObjectTypeDefinition : WorldRecord
{
    [Export] public string[] Tags {get;set;}=System.Array.Empty<string>();
    [Export] public Godot.Collections.Array<WorldCapabilityDefinition> Capabilities {get;set;}=new();
    public T Capability<T>() where T:WorldCapabilityDefinition=>System.Linq.Enumerable.OfType<T>(Capabilities).SingleOrDefault();
    public bool SupportsAdapter(string adapter)=>System.Linq.Enumerable.Any(Capabilities,c=>c!=null&&c.Adapter==adapter);
}
