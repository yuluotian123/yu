using System;
using System.Linq;
using Godot;
using GameLogic;

internal static class WorldTestObjects
{
    public static WorldObjectTypeDefinition StockType(double amount=20)=>new(){Tags=new[]{"storage"},Capabilities=new(){
        new WorldInteractionCapability(),new WorldStockCapability{InitialAmount=amount},new WorldUsageCapacityCapability(),new WorldOpeningHoursCapability{OpenHour=0,CloseHour=0}}};
    public static Component3D[] Components(WorldObjectTypeDefinition type,WorldObjectComponent3D binding=null)
    {
        binding??=new(){ObjectType=type};
        return new Component3D[]{binding}.Concat(binding.Definitions.Select(d=>(Component3D)Activator.CreateInstance(d.ComponentType))).ToArray();
    }
    public static GameObject3D WithArea(GameObject3D host)
    {
        if(!host.Components.OfType<WorldInteractionComponent3D>().Any())return host;
        if(host.GetNodeOrNull("UsePosition")==null)host.AddChild(new Marker3D{Name="UsePosition"});
        var area=new Area3D{Name="InteractionArea",CollisionLayer=0,CollisionMask=0,Monitoring=false};
        area.AddChild(new CollisionShape3D{Shape=new CylinderShape3D{Radius=.55f,Height=2}});host.AddChild(area);return host;
    }
    public static string ShippedId(WorldModule world,string name)=>world.Catalog.Objects.Values.First(o=>o.DisplayName==name).Id;
}
