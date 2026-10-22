using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
namespace GameLogic;

public abstract class WorldCapabilityState
{
    public virtual int Version=>1;
    public abstract JsonObject Capture();
    public abstract void Restore(JsonObject data);
}
public sealed class WorldEmptyCapabilityState : WorldCapabilityState
{
    public override JsonObject Capture()=>new();
    public override void Restore(JsonObject data)=>WorldCatalog.Require(data.Count==0,"无状态能力的存档应为空。");
}
public sealed class WorldStockState : WorldCapabilityState
{
    readonly Action _changed;
    double _amount;
    public double Amount {get=>_amount;set{WorldCatalog.Require(double.IsFinite(value)&&value>=0,"库存必须为有限非负数。");if(_amount!=value){_amount=value;_changed();}}}
    public WorldStockState(double initial,Action changed){_amount=initial;_changed=changed;}
    public override JsonObject Capture()=>new(){["amount"]=Amount};
    public override void Restore(JsonObject data)=>Amount=data["amount"].GetValue<double>();
}
public sealed class WorldOpeningHoursState(Action changed) : WorldCapabilityState
{
    bool? _manualOpen;
    public bool? ManualOpen {get=>_manualOpen;set{if(_manualOpen!=value){_manualOpen=value;changed();}}}
    public override JsonObject Capture()=>new(){["manual_open"]=ManualOpen};
    public override void Restore(JsonObject data)=>ManualOpen=data["manual_open"]?.GetValue<bool>();
}
public sealed class WorldUsageCapacityState(int slots) : WorldCapabilityState
{
    readonly Dictionary<string,double> _leases=new(StringComparer.Ordinal);
    public int Slots=>slots;
    void Expire(double now){if(_leases.Count==0)return;foreach(var key in _leases.Where(p=>p.Value<=now).Select(p=>p.Key).ToArray())_leases.Remove(key);}
    public int Reservations(double now){Expire(now);return _leases.Count;}
    public bool Available(string actor,double now){Expire(now);return _leases.ContainsKey(actor)||_leases.Count<Slots;}
    public bool Reserve(string actor,double now){if(!Available(actor,now))return false;_leases[actor]=now+5;return true;}
    public bool Reserved(string actor,double now){Expire(now);return _leases.ContainsKey(actor);}
    public bool ReservePersistent(string actor,double now){if(!Available(actor,now))return false;_leases[actor]=double.PositiveInfinity;return true;}
    public void Release(string actor)=>_leases.Remove(actor);
    public override JsonObject Capture()=>new();
    public override void Restore(JsonObject data){WorldCatalog.Require(data.Count==0,"容量存档不包含现场租约。");_leases.Clear();}
}

public sealed class WorldLiquidState(double limit,string liquid,double volume,Action changed) : WorldCapabilityState
{
    public double VolumeLimit=>limit;
    public string Liquid {get;private set;}=volume>0?liquid:"";
    public double Volume {get;private set;}=volume;
    public bool TryFill(string liquid,double amount)
    {
        if(string.IsNullOrWhiteSpace(liquid)||!double.IsFinite(amount)||amount<=0||amount>limit-Volume||Volume>0&&Liquid!=liquid)return false;
        Liquid=liquid;Volume+=amount;changed();return true;
    }
    public bool TryConsume(double amount)
    {
        if(!double.IsFinite(amount)||amount<=0||amount>Volume)return false;
        Volume-=amount;if(Volume==0)Liquid="";changed();return true;
    }
    public override JsonObject Capture()=>new(){["liquid"]=Liquid,["volume"]=Volume};
    public override void Restore(JsonObject data)
    {
        string liquid=data["liquid"].GetValue<string>();double amount=data["volume"].GetValue<double>();
        WorldCatalog.Require(double.IsFinite(amount)&&amount>=0&&amount<=limit&&(amount==0?liquid.Length==0:!string.IsNullOrWhiteSpace(liquid)),"液体内容存档无效。");
        Liquid=liquid;Volume=amount;changed();
    }
}
