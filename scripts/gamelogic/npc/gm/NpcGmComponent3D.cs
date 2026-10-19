using System;
using System.Linq;
using GameLogic.AI;
using Godot;
namespace GameLogic;

[GlobalClass]
public partial class NpcGmComponent3D : Component3D
{
    public override int Priority=>ComponentPriority.VFX;
    Control _page;RichTextLabel _details;Button _replan,_points,_rain;double _elapsed;int _weather;
    AiWorldComponent3D World=>AiWorldComponent3D.Find(Owner);
    public override void OnInit()
    {
        _page=Owner.GetNodeOrNull<Control>("%NpcPage");_details=Owner.GetNodeOrNull<RichTextLabel>("%NpcDetails");
        _replan=Owner.GetNodeOrNull<Button>("%NpcReplan");_points=Owner.GetNodeOrNull<Button>("%NpcToggleShop");_rain=Owner.GetNodeOrNull<Button>("%NpcRain");
        if(_replan!=null)_replan.Pressed+=Replan;if(_points!=null)_points.Pressed+=TogglePoints;if(_rain!=null)_rain.Pressed+=Rain;
    }
    public override void OnUpdate(double delta){if(_page?.IsVisibleInTree()!=true)return;_elapsed+=delta;if(_elapsed<.3)return;_elapsed=0;Refresh();}
    void Refresh(){if(_details==null)return;var w=World;_details.Text=w==null?"当前场景无 NPC 世界。":$"NPC {w.Actors.Count} · 交互点 {w.Points.Count} · 规划 {w.LastPlanningMilliseconds:0.000} ms\n"+string.Join("\n\n",w.Actors.Values.OrderBy(a=>a.ActorId,StringComparer.Ordinal).Select(a=>a.Describe()));}
    void Replan(){if(World==null)return;foreach(var a in World.Actors.Values)a.CancelPlan();Refresh();}
    void TogglePoints(){if(World==null)return;foreach(var p in World.Points.Values)p.SetOpen(!p.IsOpen);Replan();}
    void Rain(){if(World==null)return;_weather=(_weather+1)%3;World.RainOverride=_weather==0?null:_weather==1?1:0;Replan();}
    public override void OnDestroy(){if(_replan!=null)_replan.Pressed-=Replan;if(_points!=null)_points.Pressed-=TogglePoints;if(_rain!=null)_rain.Pressed-=Rain;}
}
