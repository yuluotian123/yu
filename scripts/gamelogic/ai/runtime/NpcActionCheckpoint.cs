using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameLogic.Goap;

namespace GameLogic.AI;

// Deliberately limited to replay-safe semantic primitives, never arbitrary Flow stacks.
internal sealed class NpcCheckpointLayout
{
    public readonly Dictionary<GraphSharedAction,string> Steps=new();
    public readonly Dictionary<NpcGraphNumber,string> Numbers=new();
    public static NpcCheckpointLayout Create(FlowGraphAsset graph)
    {
        if(graph?.PrimeNode is not FlowEntryNodeData||graph.Nodes.OfType<FlowEntryNodeData>().Count()!=1)return null;
        var edges=graph.Connections.Where(e=>e.IsAvailable).ToArray();
        if(edges.Any(e=>e is FlowConnection f&&(f.Mode!=FlowConnectionMode.Sequence||f.Conditions?.Count>0)))return null;
        var layout=new NpcCheckpointLayout();var visited=new HashSet<string>();var node=graph.PrimeNode;
        while(node!=null&&visited.Add(node.Id))
        {
            if(node is FlowReturnNodeData end)return end.Label=="Success"?layout:null;
            if(node is FlowActionNodeData sequence)
            {
                for(int i=0;i<sequence.Actions.Count;i++)
                {
                    var action=sequence.Actions[i];
                    if(action is not (NpcWaitAction or NpcMoveToTargetAction or NpcInteractAction or NpcCalculateNumberAction))return null;
                    // One definition used twice has ambiguous progress; use two separate graph actions.
                    if(!layout.Steps.TryAdd((GraphSharedAction)action,node.Id+":"+i))return null;
                    var number=action switch {NpcWaitAction w=>w.Seconds,NpcInteractAction x=>x.Amount,NpcCalculateNumberAction c=>c.Value,_=>null};
                    if(number!=null)layout.Numbers.TryAdd(number,node.Id+":"+i);
                }
            }
            else if(node is not FlowEntryNodeData)return null;
            var outgoing=edges.Where(e=>e.FromNode==node.Id).ToArray();
            if(outgoing.Any(e=>e.FromPort!=0&&(e.FromPort!=1||graph.FindNodeById(e.ToNode) is not FlowReturnNodeData failure||failure.Label=="Success")))return null;
            var next=outgoing.Where(e=>e.FromPort==0).ToArray();if(next.Length!=1)return null;
            node=graph.FindNodeById(next[0].ToNode);
        }
        return null;
    }
    public static string Signature(NpcProgram program)
    {
        var data=new
        {
            program.Facts,program.Parameters,program.Needs,program.Windows,program.TargetSelectors,
            Goals=program.Goals.Select(g=>new {g.Id,g.Activation,g.Utility,g.Completion,g.Plans,
                Actions=g.Actions.Select(a=>new {a.Id,a.Inputs,a.Resources,a.Precondition,a.Success,a.Cost,a.Duration,a.Timeout,a.Predictions,a.Results,
                    Graph=GraphJsonHelper.Serialize(a.Execution.Document)})})
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data,new JsonSerializerOptions{MaxDepth=256}))));
    }
}

internal sealed class NpcTaskProgress
{
    public bool Complete {get;set;}
    public double Elapsed {get;set;}
    public double? Duration {get;set;}
    public bool Valid=>double.IsFinite(Elapsed)&&Elapsed>=0&&(!Duration.HasValue||double.IsFinite(Duration.Value)&&Duration>=0);
}

internal sealed record NpcSavedFact(string Key,GoapValueKind Kind,long Integer,string Symbol)
{
    public GoapValue Read()=>Kind switch
    {
        GoapValueKind.Boolean when Integer is 0 or 1=>GoapValue.Bool(Integer!=0),
        GoapValueKind.Integer=>GoapValue.Number(Integer),
        GoapValueKind.Symbol when Symbol!=null=>GoapValue.Id(Symbol),
        _=>throw new ArgumentException("行动快照事实无效。")
    };
}

internal sealed class NpcExecutionCheckpoint
{
    public List<NpcSavedFact> Start {get;set;}=new();
    public Dictionary<string,NpcTaskProgress> Progress {get;set;}=new(StringComparer.Ordinal);
    public Dictionary<string,double> Numbers {get;set;}=new(StringComparer.Ordinal);
    public Dictionary<string,double> Locals {get;set;}=new(StringComparer.Ordinal);
}

internal sealed class NpcSavedAction
{
    public string ActionId {get;set;}="";
    public Dictionary<string,CharacterValue> Targets {get;set;}=new(StringComparer.Ordinal);
    public Dictionary<string,CharacterValue> Inputs {get;set;}
    public string InstanceId {get;set;}="";
    public double Elapsed {get;set;}
    public bool Committed {get;set;}
    public NpcExecutionCheckpoint Execution {get;set;}
}

internal sealed class NpcPlanCheckpoint
{
    public int Version {get;set;}=1;
    public string Signature {get;set;}="";
    public string Actor {get;set;}="";
    public string Level {get;set;}="";
    public string Goal {get;set;}="";
    public string Plan {get;set;}="";
    public string InstanceId {get;set;}="";
    public bool Abstract {get;set;}
    public Dictionary<string,CharacterValue> Bindings {get;set;}=new(StringComparer.Ordinal);
    public NpcSavedAction Current {get;set;}
    public List<NpcSavedAction> Remaining {get;set;}=new();
}
