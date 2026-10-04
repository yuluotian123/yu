#if CONFIG_TRANSACTION_TESTS
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Framework;

internal static class ConfigTransactionTests
{
    private sealed class Fixture
    {
        internal readonly string Root;
        internal string Json => Path.Combine(Root, "json");
        internal string Cs => Path.Combine(Root, "cs");
        internal string First => Path.Combine(Json, "a.json");
        internal string Created => Path.Combine(Json, "b.json");
        internal string Last => Path.Combine(Cs, "AConfig.cs");
        internal string[] Directories => new[] { Json, Cs };
        internal ConfigOutput[] Outputs => new[]
        {
            new ConfigOutput(First, "new-json"),
            new ConfigOutput(Created, "new-file"),
            new ConfigOutput(Last, "new-code")
        };

        internal Fixture(string root, bool initialize = true)
        {
            Root = root;
            if (!initialize) return;
            Directory.CreateDirectory(Json);
            Directory.CreateDirectory(Cs);
            File.WriteAllText(First, "old-json");
            File.WriteAllText(Last, "old-code");
        }

        internal void Recover(Action<string> checkpoint = null)
        {
            using var transaction = ConfigOutputTransaction.Begin(Directories, checkpoint);
        }

        internal string[] Journals => Directories.SelectMany(directory =>
            Directory.GetFiles(Path.Combine(directory, ConfigOutputTransaction.MetadataDirectory), "*.json")).ToArray();

        internal void AssertOriginal()
        {
            Require(File.ReadAllText(First) == "old-json" && File.ReadAllText(Last) == "old-code" && !File.Exists(Created),
                "Original batch was not fully restored.");
        }

        internal void AssertPublished()
        {
            foreach (ConfigOutput output in Outputs)
                Require(File.ReadAllText(output.Path) == output.Content, "Committed batch was changed by recovery.");
        }

        internal void AssertClean()
        {
            foreach (string directory in Directories)
            {
                Require(Directory.GetFiles(Path.Combine(directory, ConfigOutputTransaction.MetadataDirectory))
                    .All(path => Path.GetFileName(path) == "lock"), "Recovery left transaction artifacts behind.");
            }
        }
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--child")
            {
                RunChild(args[1], args[2], args[3], args[4]);
                return 0;
            }
            Require(File.Exists("yu.csproj"), "Run these tests from the yu repository root.");
            string root = Path.GetFullPath(Path.Combine("tmp", "config-process-tests-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                foreach (string checkpoint in new[] { "journal:0", "journal:1", "staged:0", "staged:2", "ready", "published:0", "published:1", "published:2", "committed", "cleanup:0" })
                {
                    Run(root, "crash-" + checkpoint.Replace(':', '-'), fixture =>
                    {
                        Crash(fixture, "publish", checkpoint);
                        fixture.Recover();
                        if (checkpoint is "committed" or "cleanup:0") fixture.AssertPublished();
                        else fixture.AssertOriginal();
                        fixture.Recover();
                        fixture.AssertClean();
                    });
                }
                Run(root, "recovery-can-crash", fixture =>
                {
                    Crash(fixture, "publish", "published:2");
                    Crash(fixture, "recover", "rollback:2");
                    fixture.Recover();
                    fixture.AssertOriginal();
                    fixture.AssertClean();
                });
                Run(root, "rolled-back-cleanup-can-crash", fixture =>
                {
                    Crash(fixture, "publish", "published:2");
                    Crash(fixture, "recover", "cleanup:0");
                    File.WriteAllText(fixture.First, "later-user-edit");
                    fixture.Recover();
                    Require(File.ReadAllText(fixture.First) == "later-user-edit", "Terminal rollback was applied twice.");
                    fixture.AssertClean();
                });
                Run(root, "require-all-directories", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    Expect<IOException>(() => { using var transaction = ConfigOutputTransaction.Begin(new[] { fixture.Json }); });
                    Require(File.ReadAllText(fixture.First) == "new-json", "Partial-directory recovery changed output.");
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "external-edit-conflict", fixture =>
                {
                    Crash(fixture, "publish", "published:2");
                    File.WriteAllText(fixture.Last, "user-edit");
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(fixture.Last) == "user-edit" && File.ReadAllText(fixture.First) == "new-json",
                        "Recovery overwrote a user edit or started a partial rollback.");
                    Require(fixture.Journals.Length == 2, "Conflicting batch lost its journals.");
                    File.WriteAllText(fixture.Last, "new-code");
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "damaged-backup", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string backup = Directory.GetFiles(Path.Combine(fixture.Json, ConfigOutputTransaction.MetadataDirectory), "*.0.old").Single();
                    byte[] original = File.ReadAllBytes(backup);
                    File.WriteAllText(backup, "damaged");
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(fixture.First) == "new-json" && File.Exists(backup), "Damaged backup was used or deleted.");
                    File.WriteAllBytes(backup, original);
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "damaged-journal", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string journal = fixture.Journals[0];
                    byte[] original = File.ReadAllBytes(journal);
                    File.WriteAllText(journal, "{broken");
                    Expect<IOException>(() => fixture.Recover());
                    Require(fixture.Journals.Length == 2, "Damaged journal was deleted.");
                    File.WriteAllBytes(journal, original);
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "damaged-ready-marker", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string marker = Directory.GetFiles(Path.Combine(fixture.Cs, ConfigOutputTransaction.MetadataDirectory), "*.ready").Single();
                    byte[] original = File.ReadAllBytes(marker);
                    File.WriteAllText(marker, "partial");
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(fixture.First) == "new-json" && fixture.Journals.Length == 2, "Damaged marker was treated as a completed transaction.");
                    File.WriteAllBytes(marker, original);
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "unfinished-rollback-marker", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string journal = fixture.Journals.Single(path => Path.GetDirectoryName(path) == Path.Combine(fixture.Cs, ConfigOutputTransaction.MetadataDirectory));
                    File.WriteAllText(Path.ChangeExtension(journal, "rolledback.tmp"), "partial");
                    fixture.Recover();
                    fixture.AssertOriginal();
                    fixture.AssertClean();
                });
                Run(root, "missing-ready-marker", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string marker = Directory.GetFiles(Path.Combine(fixture.Cs, ConfigOutputTransaction.MetadataDirectory), "*.ready").Single();
                    byte[] original = File.ReadAllBytes(marker);
                    File.Delete(marker);
                    Expect<IOException>(() => fixture.Recover());
                    Require(fixture.Journals.Length == 2 && File.ReadAllText(fixture.First) == "new-json", "Missing prepared marker discarded recovery data.");
                    File.WriteAllBytes(marker, original);
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "orphan-backup", fixture =>
                {
                    string metadata = Path.Combine(fixture.Json, ConfigOutputTransaction.MetadataDirectory);
                    Directory.CreateDirectory(metadata);
                    string backup = Path.Combine(metadata, Guid.NewGuid().ToString("N") + ".0.old");
                    File.WriteAllText(backup, "recovery-data");
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(backup) == "recovery-data", "Unexplained recovery data was discarded.");
                });
                Run(root, "unprepared-user-edit", fixture =>
                {
                    Crash(fixture, "publish", "staged:0");
                    File.WriteAllText(fixture.First, "user-edit");
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(fixture.First) == "user-edit", "Pre-publication recovery changed user data.");
                    Require(fixture.Journals.Length == 2, "Ambiguous batch lost its journals.");
                    File.WriteAllText(fixture.First, "old-json");
                    fixture.Recover();
                    fixture.AssertClean();
                });
                Run(root, "committed-user-edit", fixture =>
                {
                    Crash(fixture, "publish", "committed");
                    File.WriteAllText(fixture.First, "user-edit");
                    fixture.Recover();
                    Require(File.ReadAllText(fixture.First) == "user-edit" && File.ReadAllText(fixture.Last) == "new-code",
                        "Committed transaction was rolled back after a later edit.");
                    fixture.AssertClean();
                });
                Run(root, "journal-traversal", fixture =>
                {
                    Crash(fixture, "publish", "published:0");
                    string[] journals = fixture.Journals;
                    byte[] original = File.ReadAllBytes(journals[0]);
                    JsonNode modified = JsonNode.Parse(original);
                    modified["Files"][0]["Name"] = "../outside.txt";
                    string outside = Path.Combine(fixture.Root, "outside.txt");
                    File.WriteAllText(outside, "untouched");
                    foreach (string journal in journals) File.WriteAllText(journal, modified.ToJsonString());
                    Expect<IOException>(() => fixture.Recover());
                    Require(File.ReadAllText(outside) == "untouched", "Recovery escaped an output directory.");
                    foreach (string journal in journals) File.WriteAllBytes(journal, original);
                    fixture.Recover();
                    fixture.AssertOriginal();
                });
                Run(root, "cross-process-lock", fixture =>
                {
                    using Process child = StartPaused(fixture, "hold", "locked");
                    try
                    {
                        Expect<IOException>(() => ConfigOutputTransaction.Commit(fixture.Outputs, true));
                        Expect<IOException>(() => ConfigOutputTransaction.Commit(new[] { fixture.Outputs[0] }, true));
                        fixture.AssertOriginal();
                    }
                    finally { Kill(child); }
                    ConfigOutputTransaction.Commit(fixture.Outputs, true);
                    fixture.AssertPublished();
                    fixture.AssertClean();
                });
                Run(root, "partial-lock-release", fixture =>
                {
                    using var held = ConfigOutputTransaction.Begin(new[] { fixture.Json });
                    Expect<IOException>(() => { using var blocked = ConfigOutputTransaction.Begin(fixture.Directories); });
                    using var other = ConfigOutputTransaction.Begin(new[] { fixture.Cs });
                });
                Run(root, "write-failure-rollback", fixture =>
                {
                    using (var held = new FileStream(fixture.Last, FileMode.Open, FileAccess.Read, FileShare.Read))
                        Expect<IOException>(() => ConfigOutputTransaction.Commit(fixture.Outputs, true));
                    fixture.AssertOriginal();
                    fixture.AssertClean();
                });
                Run(root, "unchanged-output", fixture =>
                {
                    ConfigOutputTransaction.Commit(fixture.Outputs, true);
                    DateTime timestamp = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    File.SetLastWriteTimeUtc(fixture.First, timestamp);
                    ConfigOutputTransaction.Commit(fixture.Outputs, true);
                    Require(File.GetLastWriteTimeUtc(fixture.First) == timestamp, "Unchanged output was rewritten.");
                    fixture.AssertClean();
                });
                Console.WriteLine("[ConfigTransactionTests] ALL PASS");
                return 0;
            }
            finally { Directory.Delete(root, recursive: true); }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("[ConfigTransactionTests] FAIL: " + exception);
            return 1;
        }
    }

    private static void Run(string root, string name, Action<Fixture> test)
    {
        test(new Fixture(Path.Combine(root, name)));
        Console.WriteLine("PASS " + name);
    }

    private static void RunChild(string mode, string root, string checkpoint, string signal)
    {
        var fixture = new Fixture(root, initialize: false);
        void Pause(string step)
        {
            if (step != checkpoint) return;
            File.WriteAllText(signal, step);
            Thread.Sleep(Timeout.Infinite);
        }
        if (mode == "publish") ConfigOutputTransaction.Commit(fixture.Outputs, true, Pause);
        else if (mode == "recover") fixture.Recover(Pause);
        else
        {
            using var transaction = ConfigOutputTransaction.Begin(fixture.Directories);
            Pause("locked");
        }
        throw new InvalidOperationException("Child did not reach checkpoint " + checkpoint);
    }

    private static Process StartPaused(Fixture fixture, string mode, string checkpoint)
    {
        string signal = Path.Combine(fixture.Root, "signal-" + Guid.NewGuid().ToString("N"));
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach (string argument in new[] { "--child", mode, fixture.Root, checkpoint, signal })
            info.ArgumentList.Add(argument);
        Process child = Process.Start(info)!;
        var timer = Stopwatch.StartNew();
        while (!File.Exists(signal) && !child.HasExited && timer.Elapsed < TimeSpan.FromSeconds(20))
            Thread.Sleep(20);
        if (!File.Exists(signal))
        {
            Kill(child);
            string error = child.StandardError.ReadToEnd();
            child.Dispose();
            throw new InvalidOperationException("Child failed before checkpoint " + checkpoint + ": " + error);
        }
        return child;
    }

    private static void Crash(Fixture fixture, string mode, string checkpoint)
    {
        using Process child = StartPaused(fixture, mode, checkpoint);
        Kill(child);
    }

    private static void Kill(Process child)
    {
        if (!child.HasExited) child.Kill(entireProcessTree: true);
        Require(child.WaitForExit(10000), "Child process did not terminate.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
#endif
