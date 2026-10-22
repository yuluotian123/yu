#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
namespace GameLogic;

/// <summary>Editor-only materialization of scene configuration. No runtime scene scans.</summary>
public static class WorldAuthoringCompiler
{
    // Existing live nodes own their keys; duplication always changes the new node, even if inserted first.
    static readonly Dictionary<(ulong,string),ulong> LiveKeys=new();
    static readonly Dictionary<string,string> Normalized=new(StringComparer.Ordinal);
    static WorldCatalog _editorCatalog;
    public static WorldCatalog EditorCatalog=>_editorCatalog??=new(ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath));
    public static bool Prepare(Node root)
    {
        if(!GodotObject.IsInstanceValid(root))return false;
        var relevant=WorldIdentity.RelevantNodes(root);if(relevant.Count==0)return false;
        bool changed=false;var scope=root.GetInstanceId();
        var anchors=Descendants(root).Where(n=>n.Owner==root&&relevant.Contains(n)
            &&(!string.IsNullOrEmpty(n.SceneFilePath)||n is GameObject3D h&&h.Components.OfType<WorldObjectComponent3D>().Any())).ToArray();
        var used=new HashSet<string>(StringComparer.Ordinal);
        // Reserve keys of known original nodes before visiting fresh duplicates.
        foreach(var n in anchors)
        {
            string key=n.GetMeta(WorldIdentity.PlacementKey,"").AsString();
            if(LiveKeys.TryGetValue((scope,key),out ulong existing)&&existing==n.GetInstanceId())used.Add(key);
        }
        foreach(var n in anchors)
        {
            string key=n.GetMeta(WorldIdentity.PlacementKey,"").AsString();ulong id=n.GetInstanceId();
            bool owned=LiveKeys.TryGetValue((scope,key),out ulong existing)&&existing==id;
            if(!owned&&(!Guid.TryParseExact(key,"N",out _)||used.Contains(key)))
            {key=Guid.NewGuid().ToString("N");n.SetMeta(WorldIdentity.PlacementKey,key);changed=true;}
            used.Add(key);LiveKeys[(scope,key)]=id;
            if(n is GameObject3D host&&host.Components.OfType<WorldObjectComponent3D>().SingleOrDefault() is {} binding)
            {
                var before=host.Components;WorldObjectAuthoring.SynchronizeHost(binding,host);changed|=!ReferenceEquals(before,host.Components);
            }
        }
        // Template roots contain configuration only. Their placement key is owned by the parent scene.
        if(root is GameObject3D template&&template.Components.OfType<WorldObjectComponent3D>().SingleOrDefault() is {} config)
        {var before=template.Components;WorldObjectAuthoring.SynchronizeHost(config,template);changed|=!ReferenceEquals(before,template.Components);}
        return changed;
    }
    static IEnumerable<Node> Descendants(Node root)
    {using var children=(Godot.Collections.Array)root.GetChildren();foreach(Variant value in children){var child=value.As<Node>();yield return child;foreach(var next in Descendants(child))yield return next;}}
    public static void Forget(Node root)
    {if(root!=null)foreach(var key in LiveKeys.Keys.Where(k=>k.Item1==root.GetInstanceId()).ToArray())LiveKeys.Remove(key);}
    public static void CompileAll(WorldDefinition definition=null)
    {
        definition??=ResourceLoader.Load<WorldDefinition>(WorldDefinition.CommonPath);
        var normalized=new HashSet<string>(StringComparer.Ordinal);
        var relevant=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(var level in definition.Levels){NormalizeScene(level.ScenePath,normalized,relevant);Compile(level,relevant);}
        var catalog=new WorldCatalog(definition);
        if(definition.ResourcePath==WorldDefinition.CommonPath)_editorCatalog=catalog;
    }
    static bool Relevant(string path,Dictionary<string,bool> cache)
    {
        if(cache.TryGetValue(path,out bool found))return found;
        cache[path]=false;
        var deps=Dependencies(path).ToArray();
        return cache[path]=deps.Any(d=>d.EndsWith("/WorldObjectComponent3D.cs",StringComparison.Ordinal))
            ||deps.Where(d=>d.EndsWith(".tscn")||d.EndsWith(".scn")||d.EndsWith(".tres")||d.EndsWith(".res")).Any(d=>Relevant(d,cache));
    }
    static void NormalizeScene(string path,HashSet<string> visited,Dictionary<string,bool> relevant)
    {
        if(!visited.Add(path)||!Relevant(path,relevant))return;
        foreach(string dep in Dependencies(path))if(dep.EndsWith(".tscn")||dep.EndsWith(".scn"))NormalizeScene(dep,visited,relevant);
        string signature=Signature(path,relevant);
        if(Normalized.GetValueOrDefault(path)==signature)return;
        var packed=ResourceLoader.Load<PackedScene>(path,cacheMode:ResourceLoader.CacheMode.Replace);
        WorldCatalog.Require(packed!=null,"关卡或模板读取失败："+path);
        Node root=packed.Instantiate(PackedScene.GenEditState.Instance);
        try
        {
            if(Prepare(root))
            {
                var saved=new PackedScene();WorldCatalog.Require(saved.Pack(root)==Error.Ok&&ResourceSaver.Save(saved,path)==Error.Ok,"自动摆放身份保存失败："+path);
            }
            Normalized[path]=Signature(path,relevant);
        }
        finally{Forget(root);root.Free();}
    }
    public static void Compile(WorldLevelDefinition level)=>Compile(level,new(StringComparer.Ordinal));
    static void Compile(WorldLevelDefinition level,Dictionary<string,bool> relevant)
    {
        string signature=Signature(level.ScenePath,relevant)+"/"+level.Id+"/"+level.Location?.Id;
        if(ResourceLoader.Exists(level.CatalogPath))
        {
            var old=ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath,cacheMode:ResourceLoader.CacheMode.Replace);
            if(old?.SourceSignature==signature&&old.Version==1&&old.LevelId==level.Id)return;
        }
        Node root=Relevant(level.ScenePath,relevant)?ResourceLoader.Load<PackedScene>(level.ScenePath,cacheMode:ResourceLoader.CacheMode.Replace).Instantiate(PackedScene.GenEditState.Instance):null;
        try
        {
            var catalog=new WorldLevelCatalog{LevelId=level.Id,SourceSignature=signature};
            foreach(var pair in root!=null?WorldIdentity.Resolve(root):new Dictionary<GameObject3D,string>())
            {
                var binding=pair.Key.Components.OfType<WorldObjectComponent3D>().Single();
                WorldCatalog.ValidateOverrides(binding);binding.ValidateComponents(pair.Key.Components,pair.Key);
                using var data=binding.Snapshot(pair.Value,level,pair.Key.Name).Encode();catalog.Entries.Add(data);
            }
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(level.CatalogPath.GetBaseDir()));
            WorldCatalog.Require(ResourceSaver.Save(catalog,level.CatalogPath)==Error.Ok,"自动世界目录保存失败："+level.DisplayName);
            // Replace an existing cached resource so runtime and inspectors immediately see the new directory.
            _=ResourceLoader.Load<WorldLevelCatalog>(level.CatalogPath,cacheMode:ResourceLoader.CacheMode.Replace);
        }
        finally{root?.Free();}
    }
    static IEnumerable<string> Dependencies(string path)=>ResourceLoader.GetDependencies(path).Select(d=>d.Split("::").Last()).Where(d=>d.StartsWith("res://",StringComparison.Ordinal));
    static string Signature(string path,Dictionary<string,bool> relevant)
    {
        var seen=new HashSet<string>(StringComparer.Ordinal);var text=new StringBuilder();
        void Append(string file)
        {
            if(!seen.Add(file))return;text.Append(file).Append(Convert.ToHexString(SHA256.HashData(FileAccess.GetFileAsBytes(file))));
            foreach(var dep in Dependencies(file).OrderBy(p=>p,StringComparer.Ordinal))
                if((dep.EndsWith(".tscn")||dep.EndsWith(".scn"))?Relevant(dep,relevant)
                    :(dep.EndsWith(".tres")||dep.EndsWith(".res"))&&Dependencies(dep).Any(d=>d.Contains("/world/",StringComparison.Ordinal)))Append(dep);
        }
        Append(path);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
#endif
