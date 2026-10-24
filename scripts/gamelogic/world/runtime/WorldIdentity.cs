using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic;

/// <summary>IDs contain saved placement keys only, never node names or transforms.</summary>
public static class WorldIdentity
{
    public const string PlacementKey="_world_placement";
    public static bool HasObjects(Node root)=>WorldModule.Hosts(root).Any(h=>h.Components.OfType<WorldObjectComponent3D>().Any());
    internal static HashSet<Node> RelevantNodes(Node root,bool portals=false)
    {
        var nodes=new HashSet<Node>();
        bool Collect(Node node)
        {
            bool found=node is GameObject3D host&&(portals?host.Components.OfType<WorldTransitionComponent3D>().Any():host.Components.OfType<WorldObjectComponent3D>().Any());
            using var children=(Godot.Collections.Array)node.GetChildren();foreach(Variant child in children)found=Collect(child.As<Node>())||found;
            if(found)nodes.Add(node);return found;
        }
        Collect(root);return nodes;
    }
    public static IReadOnlyDictionary<GameObject3D,string> Resolve(Node root,bool portals=false)
    {
        var result=new Dictionary<GameObject3D,string>();var ids=new HashSet<string>(StringComparer.Ordinal);var relevant=RelevantNodes(root,portals);
        void Visit(Node node,string prefix)
        {
            bool obj=node is GameObject3D h&&(portals?h.Components.OfType<WorldTransitionComponent3D>().Any():h.Components.OfType<WorldObjectComponent3D>().Any());
            bool anchor=node!=root&&(obj||!string.IsNullOrEmpty(node.SceneFilePath));
            if(!relevant.Contains(node))return;
            if(anchor||node==root&&obj)
            {
                string key=node.GetMeta(PlacementKey,"").AsString();
                WorldCatalog.Require(Guid.TryParseExact(key,"N",out _),"世界对象缺少自动摆放身份，请保存关卡："+node.Name);
                prefix=prefix.Length==0?key:prefix+"/"+key;
            }
            if(obj)
            {
                string id=(portals?"portal/":"world/")+prefix;WorldCatalog.Require(ids.Add(id),"场景有重复摆放身份："+node.Name);result.Add((GameObject3D)node,id);
            }
            using var children=(Godot.Collections.Array)node.GetChildren();foreach(Variant child in children)Visit(child.As<Node>(),prefix);
        }
        Visit(root,"");return result;
    }
    public static void Bind(Node root){foreach(var pair in Resolve(root))pair.Key.PersistentId=pair.Value;}
}
