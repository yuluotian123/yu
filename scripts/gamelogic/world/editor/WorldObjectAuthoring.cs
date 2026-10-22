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
        // Only the list changes. Keep configuration resources and the shared type linked to their source.
        host.Components=new(host.Components);
        foreach(var component in host.Components.Where(c=>c is IWorldCapabilityProvider p&&!definitions.Any(d=>d.Key==p.CapabilityKey&&d.ComponentType.IsInstanceOfType(c))).ToArray())host.Components.Remove(component);
        foreach(var definition in definitions)if(!host.Components.Any(c=>definition.ComponentType.IsInstanceOfType(c)))host.Components.Add((Component3D)Activator.CreateInstance(definition.ComponentType));
        host.NotifyPropertyListChanged();return binding;
    }
}
#endif
