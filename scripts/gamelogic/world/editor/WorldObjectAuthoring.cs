#if TOOLS
using System;
using System.Linq;
using Godot;
namespace GameLogic;

public static class WorldObjectAuthoring
{
    public static WorldObjectComponent3D SynchronizeHost(WorldObjectComponent3D binding,GameObject3D host)
    {
        WorldCatalog.Require(GodotObject.IsInstanceValid(host)&&binding.ObjectType!=null,"请在宿主选择 ObjectType。");
        WorldCatalog.ValidateOverrides(binding);
        var definitions=binding.Definitions.ToArray();
        if(definitions.All(d=>host.Components.Count(c=>d.ComponentType.IsInstanceOfType(c))==1)
            &&host.Components.OfType<IWorldCapabilityProvider>().All(p=>definitions.Any(d=>d.Key==p.CapabilityKey)))return binding;
        // Publish the complete list in one assignment; editor serialization may observe the setter.
        var next=new Godot.Collections.Array<Component3D>(host.Components);
        foreach(var component in next.Where(c=>c is IWorldCapabilityProvider p&&!definitions.Any(d=>d.Key==p.CapabilityKey&&d.ComponentType.IsInstanceOfType(c))).ToArray())next.Remove(component);
        foreach(var definition in definitions)if(!next.Any(c=>definition.ComponentType.IsInstanceOfType(c)))next.Add((Component3D)Activator.CreateInstance(definition.ComponentType));
        binding.ValidateComponents(next);host.Components=next;
        host.NotifyPropertyListChanged();return binding;
    }
}
#endif
