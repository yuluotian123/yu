using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Framework;
using GameLogic;
using Godot;
using File = System.IO.File;

public partial class GraphConfigReliabilitySmokeTest : Node
{
    public override void _Ready() => Callable.From(Run).CallDeferred();

    private async void Run()
    {
        try
        {
            VerifyGraphFailures();
            VerifyTypeCompatibility();
            VerifyIndexUpdates();
            VerifyExistingGraphs();
            VerifyMissionProgression();
            VerifyConfigTransactions();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("[GraphConfigReliabilitySmokeTest] PASS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[GraphConfigReliabilitySmokeTest] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyGraphFailures()
    {
        string[] invalidDocuments =
        {
            "{broken", "null", "[]", "{\"SchemaVersion\":99}",
            "{\"SchemaVersion\":null}", "{\"$type\":\"MissingDocument\"}",
            "{\"Nodes\":{}}", "{\"ActionDependencyMode\":\"InvalidMode\"}",
            "{\"ActionDependencyMode\":\"999\"}",
            "{\"Nodes\":[{\"$type\":\"MissingNode\",\"CustomData\":42}]}",
            "{\"Nodes\":[{\"$type\":\"GraphEditorState\"}]}",
            "{\"Nodes\":[{\"$type\":\"GraphNodeData\",\"Id\":42}]}"
        };
        foreach (string json in invalidDocuments)
        {
            using var graph = new GraphAsset { GraphJson = json };
            Require(!graph.TryLoadDocument(out string error) && !string.IsNullOrWhiteSpace(error), "Bad graph was accepted.");
            Require(!graph.Validate(out _), "Bad graph passed validation.");
            Expect<JsonException>(graph.SaveJsonFields);
#if TOOLS
            Require(!GraphSaveService.SaveGraphResource(null, graph, false), "Bad graph reached resource saving.");
#endif
            Require(graph.GraphJson == json, "Original bad JSON was overwritten.");
            graph.GraphJson = "{}";
            Require(graph.TryLoadDocument(out _) && graph.Nodes.Count == 0, "Corrected graph could not be reloaded.");
        }

        using var flow = new FlowGraphAsset { GraphJson = "{broken" };
        var runtime = new FlowGraphRuntime(flow);
        Require(!runtime.Start() && !runtime.StartFromNode(new GraphNodeData()), "Corrupt flow graph started.");
        using var state = new StateGraphAsset { GraphJson = "{broken" };
        Require(!new StateGraphRuntime(state).Start(), "Corrupt state graph started.");
        using var behavior = new BehaviorTreeGraphAsset { GraphJson = "{broken" };
        Require(!new BehaviorTreeRuntime(behavior).Start(), "Corrupt behavior tree started.");
        using var character = new GameLogic.CharacterGraphAsset { GraphJson = "{broken" };
        var characterRuntime = new GameLogic.CharacterGraphRuntime(character, null, null);
        Require(!characterRuntime.IsRunning, "Corrupt character graph started.");
        characterRuntime.Update(0.1, false);

#if TOOLS
        using var snapshot = new GraphAsset();
        snapshot.Nodes.Add(new GraphNodeData { Id = "keep" });
        Expect<JsonException>(() => GraphSnapshotService.Restore(snapshot, null, null, null, null,
            "[]", "[{\"$type\":\"MissingConnection\"}]"));
        Require(snapshot.FindNodeById("keep") != null, "Failed undo snapshot cleared the graph.");
#endif
    }

    private static void VerifyTypeCompatibility()
    {
        const string legacy = "{\"$type\":\"GraphDocument\",\"Nodes\":[{\"$type\":\"GraphNodeData\",\"Id\":\"legacy\"}]}";
        GraphDocument document = GraphJsonHelper.Deserialize<GraphDocument>(legacy);
        Require(document.Nodes.Single().Id == "legacy", "Legacy short type name did not load.");
        string serialized = GraphJsonHelper.Serialize(document);
        Require(JsonNode.Parse(serialized)!["$type"]!.GetValue<string>() == "yu:GraphDocument", "Qualified type ID was not written.");
        Require(GraphJsonHelper.Deserialize<GraphDocument>(serialized).Nodes.Count == 1, "Document round trip failed.");
        GraphDocument extended = GraphJsonHelper.Deserialize<GraphDocument>("{\"LegacyField\":{\"nested\":[1,2]},\"Nodes\":[{\"$type\":\"GraphNodeData\",\"LegacyNodeField\":42}]}");
        JsonNode extensionJson = JsonNode.Parse(GraphJsonHelper.Serialize(extended));
        Require(extensionJson!["LegacyField"]!["nested"]![1]!.GetValue<int>() == 2 &&
            extensionJson["Nodes"]![0]!["LegacyNodeField"]!.GetValue<int>() == 42, "Unknown fields were lost while saving.");

        string left = GraphJsonHelper.Serialize(new GraphDataTests.Left.SameName { Value = 3 });
        string right = GraphJsonHelper.Serialize(new GraphDataTests.Right.SameName { Value = 8 });
        Require(left != right && GraphJsonHelper.Deserialize<GraphDataTests.Left.SameName>(left).Value == 3 &&
            GraphJsonHelper.Deserialize<GraphDataTests.Right.SameName>(right).Value == 8, "Namespaced types collided.");
        Expect<JsonException>(() => GraphJsonHelper.Deserialize<object>("{\"$type\":\"SameName\"}"));
        Expect<JsonException>(() => GraphJsonHelper.Deserialize<GraphNodeData>(left));
        GraphTypeRegistry.RegisterAlias("SmokeOldPayload", "SmokeNewPayload");
        GraphTypeRegistry.RegisterAlias("SmokeNewPayload", GraphTypeRegistry.GetSerializationId(typeof(GraphDataTests.Left.SameName)));
        Require(GraphJsonHelper.Deserialize<GraphDataTests.Left.SameName>("{\"$type\":\"SmokeOldPayload\",\"Value\":7}").Value == 7,
            "Alias chain was not applied.");
        Expect<ArgumentException>(() => GraphTypeRegistry.RegisterAlias("SmokeAliasCycle", "SmokeAliasCycle"));
        GraphTypeRegistry.RegisterAlias("SmokeLegacyDelay", GraphTypeRegistry.GetSerializationId(typeof(FlowDelayNodeData)));
        var aliasedNode = GraphJsonHelper.Deserialize<GraphNodeData>("{\"$type\":\"SmokeLegacyDelay\",\"NodeType\":\"SmokeLegacyDelay\"}");
        Require(aliasedNode is FlowDelayNodeData && GraphTypeRegistry.TryGetNodeDefinition(aliasedNode.NodeType, out _),
            "A renamed node's NodeType did not resolve to its definition.");
        string stable = GraphJsonHelper.Serialize(new GraphDataTests.StablePayload { Value = 5 });
        Require(stable.Contains("smoke.stable-payload", StringComparison.Ordinal), "Explicit stable ID was ignored.");
        Require(GraphJsonHelper.Deserialize<GraphDataTests.StablePayload>(stable).Value == 5, "Stable ID did not load.");
        Expect<JsonException>(() => GraphJsonHelper.Serialize(new GraphDataTests.DuplicateIdOne()));
    }

    private static void VerifyIndexUpdates()
    {
        using var graph = new GraphAsset();
        var first = new GraphNodeData { Id = "a" };
        var second = new GraphNodeData { Id = "b" };
        graph.Nodes.Add(first);
        GraphRuntimeIndex index = graph.GetRuntimeIndex();
        Require(index.FindNodeById("a") == first, "Initial index failed.");
        graph.Document.Nodes.Add(second);
        Require(index.FindNodeById("b") == second, "Direct document Add left stale index.");
        second.Id = "c";
        Require(index.FindNodeById("b") == null && index.FindNodeById("c") == second, "Node ID update left stale index.");
        var connection = new GraphConnection { FromNode = "a", ToNode = "c" };
        graph.Document.Connections.Add(connection);
        Require(index.GetOutgoingConnections("a").Count == 1, "Connection Add left stale index.");
        connection.FromNode = "c";
        connection.ToNode = "a";
        connection.FromPort = 2;
        Require(index.GetOutgoingConnections("a").Count == 0 && index.GetIncomingConnections("a").Count == 1 &&
            index.GetOutgoingConnections("c", 2).Count == 1, "Connection edit left stale index.");
        graph.Connections.RemoveAt(0);
        Require(index.GetOutgoingConnections("c").Count == 0, "RemoveAt left stale index.");
        graph.Nodes[0] = new GraphNodeData { Id = "replacement" };
        Require(index.FindNodeById("a") == null && index.FindNodeById("replacement") != null, "Indexer replacement left stale index.");
        graph.Document.Nodes = new List<GraphNodeData> { first };
        Require(index.FindNodeById("replacement") == null && index.FindNodeById("a") == first, "List replacement left stale index.");
        graph.Nodes.Clear();
        Require(index.FindNodeById("a") == null, "Clear left stale index.");
        graph.GraphJson = "{\"Nodes\":[{\"$type\":\"GraphNodeData\",\"Id\":\"new-document\"}]}";
        Require(index.FindNodeById("new-document") != null, "JSON replacement left retained index stale.");
        graph.SaveJsonFields();
        Require(GraphJsonHelper.Deserialize<GraphDocument>(graph.GraphJson).Nodes.Count == 1, "Observable collections did not serialize as arrays.");
    }

    private static void VerifyExistingGraphs()
    {
        string[] paths =
        {
            "res://assets/graphs/ai_patrol_behavior_tree.tres",
            "res://assets/graphs/character_locomotion_hfsm.tres",
            "res://assets/graphs/missiongraph_runtime_smoke.tres",
            "res://assets/graphs/missiongraph_runtime_smoke_child.tres",
            "res://assets/abilities/attack_timeline.tres",
            "res://assets/abilities/dash_timeline.tres"
        };
        foreach (string path in paths)
        {
            using GraphAsset graph = ResourceLoader.Load<GraphAsset>(path, cacheMode: ResourceLoader.CacheMode.Ignore);
            Require(graph != null, $"Existing graph resource did not load: {path}");
            Require(graph.TryLoadDocument(out string error), $"Existing graph did not deserialize: {path}: {error}");
            string serialized = GraphJsonHelper.Serialize(graph.Document);
            GraphDocument restored = GraphJsonHelper.Deserialize<GraphDocument>(serialized);
            Require(restored.Nodes.Count == graph.Nodes.Count && restored.Connections.Count == graph.Connections.Count,
                $"Existing graph lost data: {path}");
        }
    }

    private static void VerifyConfigTransactions()
    {
        string root = ProjectSettings.GlobalizePath("res://tmp/graph-config-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string input = Path.Combine(root, "input");
            string json = Path.Combine(root, "json");
            string cs = Path.Combine(root, "cs");
            Directory.CreateDirectory(input);
            string table = Path.Combine(input, "a.xlsx");
            WriteTable(table, "string", "original", sparse: true);
            XlsxTableData data = new XlsxReader().Read(table);
            Require(data.DataRows[0]["value"] == "original" && data.SourceRows[0] == 5,
                "Sparse columns, missing comment row or inline strings were read incorrectly.");
            File.WriteAllText(Path.Combine(input, "~$ignored.xlsx"), "not a workbook");
            var converter = new XlsxConverter();
            XlsxConvertResult[] results = converter.ConvertDirectory(input, json, cs);
            Require(results.Length == 1 && results[0] != null, "Excel temporary file produced a null result.");
            Require(converter.RecoverOutputs(json, cs) == 0, "Clean conversion left a pending batch.");
            using (var held = ConfigOutputTransaction.Begin(new[] { json, cs }))
                Expect<IOException>(() => converter.ConvertDirectory(input, json, cs));
            string jsonPath = Path.Combine(json, "a.json");
            string csPath = Path.Combine(cs, "AConfig.cs");
            string oldJson = File.ReadAllText(jsonPath);
            string oldCs = File.ReadAllText(csPath);
            DateTime stableTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(jsonPath, stableTime);
            File.SetLastWriteTimeUtc(csPath, stableTime);
            converter.ConvertDirectory(input, json, cs);
            Require(File.GetLastWriteTimeUtc(jsonPath) == stableTime && File.GetLastWriteTimeUtc(csPath) == stableTime,
                "Identical generation rewrote output files.");

            WriteTable(table, "string", "changed");
            string invalid = Path.Combine(input, "z.xlsx");
            WriteTable(invalid, "unknown", "bad");
            Expect<InvalidOperationException>(() => converter.ConvertDirectory(input, json, cs));
            Require(File.ReadAllText(jsonPath) == oldJson && File.ReadAllText(csPath) == oldCs &&
                !File.Exists(Path.Combine(json, "z.json")), "Invalid batch partially replaced config files.");
            WriteTable(invalid, "int", "not-a-number");
            Expect<InvalidOperationException>(() => converter.ConvertDirectory(input, json, cs));
            Require(File.ReadAllText(jsonPath) == oldJson, "Malformed number changed a previous table.");
            File.Delete(invalid);
            Expect<IOException>(() => converter.ConvertDirectory(input, json, cs, overwrite: false));
            Require(File.ReadAllText(jsonPath) == oldJson, "Overwrite=false changed output.");
            converter.ConvertDirectory(input, json, cs);
            Require(File.ReadAllText(jsonPath).Contains("changed", StringComparison.Ordinal), "Valid batch did not publish.");

            string collisions = Path.Combine(root, "collisions");
            Directory.CreateDirectory(collisions);
            WriteTable(Path.Combine(collisions, "a-b.xlsx"), "int", "1");
            WriteTable(Path.Combine(collisions, "a_b.xlsx"), "int", "2");
            Expect<IOException>(() => converter.ConvertDirectory(collisions, json, cs));
            Require(!File.Exists(Path.Combine(json, "a-b.json")), "Output collision was discovered after publication.");

            VerifyRollback(root);
            VerifyConfigValidation();
            Require(!Directory.EnumerateFiles(root, "*.config-*", SearchOption.AllDirectories).Any(), "Transaction left temporary files behind.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyMissionProgression()
    {
        using MissionGraph graph = ResourceLoader.Load<MissionGraph>("res://assets/graphs/missiongraph_runtime_smoke.tres",
            cacheMode: ResourceLoader.CacheMode.Ignore);
        var manager = new MissionManager<object>();
        var chains = new MissionChainManager(manager);
        manager.AddComponent(chains);
        try
        {
            Require(chains.StartChain(graph, "GraphDataSmoke") != null, "Mission graph did not start.");
            Require(manager.GetMissions().Length == 2, "Parallel missions were not deployed.");
            manager.SendMessage(new GameMessage(GameEventType.A));
            manager.SendMessage(new GameMessage(GameEventType.B));
            Require(manager.GetMissions().Length == 2, "Counted mission completed too early.");
            manager.SendMessage(new GameMessage(GameEventType.B));
            manager.SendMessage(new GameMessage(GameEventType.C));
            manager.SendMessage(new GameMessage(GameEventType.D));
            Require(manager.GetMissions().Length == 0 && chains.CreateRuntimeStates().Count == 0,
                "Mission sequence/subgraph did not complete.");
        }
        finally
        {
            chains.LoadChains(null);
        }
    }

    private static void VerifyRollback(string root)
    {
        string first = Path.Combine(root, "first.txt");
        string created = Path.Combine(root, "created.txt");
        string locked = Path.Combine(root, "locked.txt");
        File.WriteAllText(first, "old-first");
        File.WriteAllText(locked, "old-locked");
        // Deny deletion/replacement while still permitting the transaction's read preflight.
        using (var held = new FileStream(locked, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read))
        {
            Expect<IOException>(() => ConfigOutputTransaction.Commit(new[]
            {
                new ConfigOutput(first, "new-first"),
                new ConfigOutput(created, "new-file"),
                new ConfigOutput(locked, "new-locked")
            }, overwrite: true));
        }
        Require(File.ReadAllText(first) == "old-first" && File.ReadAllText(locked) == "old-locked" && !File.Exists(created),
            "Publication failure did not roll back replaced and newly created files.");
    }

    private static void VerifyConfigValidation()
    {
        var generator = new CSharpCodeGenerator();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var duplicate = new XlsxTableData(new[]
        {
            new XlsxFieldDef("id", "int", ""),
            new XlsxFieldDef("foo_bar", "int", ""),
            new XlsxFieldDef("foo-bar", "int", "")
        }, rows);
        Expect<ArgumentException>(() => generator.Generate("example", duplicate));
        var empty = new XlsxTableData(new[] { new XlsxFieldDef("id", "unknown", "") }, rows);
        Expect<NotSupportedException>(() => generator.Generate("example", empty));
        var valid = new XlsxTableData(new[] { new XlsxFieldDef("id", "int", "") }, rows);
        Expect<ArgumentException>(() => generator.Generate("example", valid, "class.Bad"));
        Expect<FormatException>(() => ConfigTypeRegistry.ParseCell("maybe", "bool"));
        Expect<FormatException>(() => ConfigTypeRegistry.ParseCell("NaN", "float"));
        Expect<FormatException>(() => ConfigTypeRegistry.ParseCell("oops", "ref<test>"));
        var duplicateIds = new XlsxTableData(valid.Fields.ToList(), new List<IReadOnlyDictionary<string, string>>
        {
            new Dictionary<string, string> { ["id"] = "1" },
            new Dictionary<string, string> { ["id"] = "1" }
        });
        Expect<FormatException>(() => new JsonDataWriter().Generate(duplicateIds));
    }

    private static void WriteTable(string path, string type, string value, bool sparse = false)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        WorkbookPart workbook = document.AddWorkbookPart();
        workbook.Workbook = new Workbook();
        WorksheetPart worksheet = workbook.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        worksheet.Worksheet = new Worksheet(data);
        var sheets = workbook.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet { Id = workbook.GetIdOfPart(worksheet), SheetId = 1, Name = "Data" });
        string column = sparse ? "C" : "B";
        data.Append(new Row(InlineCell("A1", "id"), InlineCell(column + "1", "value")) { RowIndex = 1 });
        data.Append(new Row(InlineCell("A2", "int"), InlineCell(column + "2", type)) { RowIndex = 2 });
        data.Append(new Row(InlineCell("A5", "1"), InlineCell(column + "5", value)) { RowIndex = 5 });
        worksheet.Worksheet.Save();
        workbook.Workbook.Save();
    }

    private static Cell InlineCell(string reference, string value) => new()
    {
        CellReference = reference,
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value))
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}

namespace GraphDataTests.Left
{
    public sealed class SameName { public int Value { get; set; } }
}

namespace GraphDataTests.Right
{
    public sealed class SameName { public int Value { get; set; } }
}

namespace GraphDataTests
{
    [GraphSerializationId("smoke.stable-payload")]
    public sealed class StablePayload { public int Value { get; set; } }

    [GraphSerializationId("smoke.duplicate-id")]
    public sealed class DuplicateIdOne { }

    [GraphSerializationId("smoke.duplicate-id")]
    public sealed class DuplicateIdTwo { }
}
