using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Framework
{
    internal sealed record ConfigOutput(string Path, string Content);

    /// <summary>Locks output directories and recovers interrupted batches before publication.</summary>
    internal sealed class ConfigOutputTransaction : IDisposable
    {
        internal const string MetadataDirectory = ".config-converter";
        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        private readonly List<FileStream> _locks = new();
        private readonly string[] _directories;
        private readonly Action<string> _checkpoint;
        private bool _published;
        private bool _disposed;

        private sealed class Journal
        {
            public int Version { get; set; } = 1;
            public string Id { get; set; }
            public string[] Directories { get; set; }
            public List<Entry> Files { get; set; } = new();
        }

        private sealed class Entry
        {
            public string Directory { get; set; }
            public string Name { get; set; }
            public string OldHash { get; set; }
            public string NewHash { get; set; }
        }

        private sealed record PendingFile(Entry Entry, byte[] Original, byte[] Content);

        private ConfigOutputTransaction(string[] directories, Action<string> checkpoint)
        {
            _directories = directories;
            _checkpoint = checkpoint;
        }

        internal int RecoveredTransactions { get; private set; }

        internal static ConfigOutputTransaction Begin(IEnumerable<string> directories, Action<string> checkpoint = null)
        {
            string[] normalized = directories?.Where(path => path != null).Select(NormalizeDirectory)
                .Distinct(PathComparer).OrderBy(path => path, PathComparer).ToArray();
            if (normalized == null || normalized.Length == 0)
                throw new ArgumentException("At least one config output directory is required.", nameof(directories));
            var transaction = new ConfigOutputTransaction(normalized, checkpoint);
            try
            {
                foreach (string directory in normalized)
                {
                    string metadata = Path.Combine(directory, MetadataDirectory);
                    EnsureNoLinks(metadata);
                    Directory.CreateDirectory(metadata);
                    string lockPath = Path.Combine(metadata, "lock");
                    EnsureNoLinks(lockPath);
                    try
                    {
                        // Never unlink this file: that would let a second process lock a different inode.
                        transaction._locks.Add(new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                    }
                    catch (IOException exception)
                    {
                        throw new IOException($"Cannot lock config output '{directory}'. Another converter may be using it.", exception);
                    }
                }
                transaction.RecoverPending();
                return transaction;
            }
            catch
            {
                transaction.Dispose();
                throw;
            }
        }

        internal static void Commit(IReadOnlyList<ConfigOutput> outputs, bool overwrite, Action<string> checkpoint = null)
        {
            ArgumentNullException.ThrowIfNull(outputs);
            if (outputs.Count == 0)
                return;
            using var transaction = Begin(outputs.Select(output => Path.GetDirectoryName(Path.GetFullPath(output.Path))), checkpoint);
            transaction.Publish(outputs, overwrite);
        }

        internal void Publish(IReadOnlyList<ConfigOutput> outputs, bool overwrite)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_published)
                throw new InvalidOperationException("A config transaction can publish only once.");
            _published = true;
            var paths = new HashSet<string>(PathComparer);
            var pending = new List<PendingFile>();
            foreach (ConfigOutput output in outputs)
            {
                string path = Path.GetFullPath(output.Path);
                string directory = NormalizeDirectory(Path.GetDirectoryName(path));
                if (!_directories.Contains(directory, PathComparer))
                    throw new IOException($"Output is outside the locked directories: '{path}'.");
                if (!paths.Add(path))
                    throw new IOException($"Multiple config outputs target '{path}'.");
                ValidateName(Path.GetFileName(path));
                EnsureNoLinks(path);
                if (Directory.Exists(path))
                    throw new IOException($"Config output is a directory: '{path}'.");
                bool exists = File.Exists(path);
                if (exists && !overwrite)
                    throw new IOException($"Config output exists and Overwrite=false: '{path}'.");
                byte[] original = exists ? File.ReadAllBytes(path) : null;
                if (original != null)
                {
                    using var reader = new StreamReader(new MemoryStream(original), Encoding.UTF8, true);
                    if (reader.ReadToEnd() == output.Content)
                        continue;
                }
                byte[] content = Encoding.UTF8.GetBytes(output.Content);
                pending.Add(new PendingFile(new Entry
                {
                    Directory = directory, Name = Path.GetFileName(path),
                    OldHash = original == null ? null : Hash(original), NewHash = Hash(content)
                }, original, content));
            }
            if (pending.Count == 0)
                return;

            var journal = new Journal
            {
                Id = Guid.NewGuid().ToString("N"), Directories = _directories,
                Files = pending.Select(file => file.Entry).ToList()
            };
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(journal);
            try
            {
                for (int i = 0; i < _directories.Length; i++)
                {
                    WriteAtomic(JournalPath(_directories[i], journal.Id), manifest);
                    _checkpoint?.Invoke($"journal:{i}");
                }
                for (int i = 0; i < pending.Count; i++)
                {
                    PendingFile file = pending[i];
                    if (file.Original != null)
                        WriteDurable(PayloadPath(journal, i, "old"), file.Original);
                    WriteDurable(PayloadPath(journal, i, "new"), file.Content);
                    _checkpoint?.Invoke($"staged:{i}");
                }
                // No output may change until every original and replacement is durable.
                WriteAtomic(MarkerPath(journal, "ready"), Encoding.ASCII.GetBytes(Hash(manifest)));
                _checkpoint?.Invoke("ready");
                for (int i = 0; i < pending.Count; i++)
                {
                    Entry file = pending[i].Entry;
                    string target = TargetPath(file);
                    if (CurrentHash(target) != file.OldHash)
                        throw new IOException($"Config output changed during conversion: '{target}'.");
                    ReplaceOrMove(PayloadPath(journal, i, "new"), target);
                    _checkpoint?.Invoke($"published:{i}");
                }
                WriteAtomic(MarkerPath(journal, "committed"), Encoding.ASCII.GetBytes(Hash(manifest)));
                _checkpoint?.Invoke("committed");
                Cleanup(journal);
            }
            catch (Exception failure)
            {
                try { Recover(journal, manifest); }
                catch (Exception recoveryFailure)
                {
                    throw new AggregateException($"Config recovery is pending. Keep the journal and backups in '{MetadataDirectory}' and retry recovery.",
                        failure, recoveryFailure);
                }
                throw;
            }
        }

        private void RecoverPending()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string directory in _directories)
            {
                foreach (string path in Directory.GetFiles(Path.Combine(directory, MetadataDirectory), "*.json").OrderBy(path => path, PathComparer))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (!seen.Add(id))
                        continue;
                    EnsureNoLinks(path);
                    byte[] manifest = File.ReadAllBytes(path);
                    Journal journal;
                    try { journal = JsonSerializer.Deserialize<Journal>(manifest); }
                    catch (JsonException exception) { throw new IOException($"Invalid config recovery journal '{path}'. Keep it for manual recovery.", exception); }
                    ValidateJournal(journal, id, directory);
                    foreach (string participant in journal.Directories)
                    {
                        string replica = JournalPath(participant, id);
                        EnsureNoLinks(replica);
                        if (File.Exists(replica) && !File.ReadAllBytes(replica).AsSpan().SequenceEqual(manifest))
                            throw new IOException($"Config journal replicas disagree: '{replica}'. Recovery was stopped.");
                    }
                    Recover(journal, manifest);
                    RecoveredTransactions++;
                }
            }
            CleanupOrphans();
        }

        private void Recover(Journal journal, byte[] manifest)
        {
            bool ready = HasMarker(journal, "ready", manifest);
            bool committed = HasMarker(journal, "committed", manifest);
            bool rolledBack = HasMarker(journal, "rolledback", manifest);
            if (committed && rolledBack)
                throw new IOException($"Conflicting config transaction markers: {journal.Id}.");
            if (!ready && !committed && !rolledBack && journal.Files.Any(file => CurrentHash(TargetPath(file)) != file.OldHash))
                throw new IOException($"Config transaction '{journal.Id}' has no prepared marker but outputs changed. Recovery data was retained for inspection.");
            if (committed || rolledBack || !ready)
            {
                Cleanup(journal);
                return;
            }

            // Verify the entire rollback first; never overwrite later user edits.
            for (int i = 0; i < journal.Files.Count; i++)
            {
                Entry file = journal.Files[i];
                string current = CurrentHash(TargetPath(file));
                if (current == file.OldHash)
                    continue;
                if (current != null && current != file.NewHash)
                    throw new IOException($"Config recovery conflict: '{TargetPath(file)}' was modified externally. Backups remain in '{MetadataDirectory}'.");
                if (file.OldHash != null && CurrentHash(PayloadPath(journal, i, "old")) != file.OldHash)
                    throw new IOException($"Config recovery backup is missing or damaged: '{PayloadPath(journal, i, "old")}'.");
            }
            for (int i = journal.Files.Count - 1; i >= 0; i--)
            {
                Entry file = journal.Files[i];
                string target = TargetPath(file);
                string current = CurrentHash(target);
                if (current == file.OldHash)
                    continue;
                if (current != null && current != file.NewHash)
                    throw new IOException($"Config output changed during recovery: '{target}'.");
                if (file.OldHash == null)
                    File.Delete(target);
                else
                {
                    byte[] original = File.ReadAllBytes(PayloadPath(journal, i, "old"));
                    if (Hash(original) != file.OldHash)
                        throw new IOException($"Config backup changed during recovery: '{target}'.");
                    string restore = PayloadPath(journal, i, "restore");
                    DeleteArtifact(restore);
                    WriteDurable(restore, original);
                    ReplaceOrMove(restore, target);
                }
                _checkpoint?.Invoke($"rollback:{i}");
            }
            WriteAtomic(MarkerPath(journal, "rolledback"), Encoding.ASCII.GetBytes(Hash(manifest)));
            Cleanup(journal);
        }

        private void ValidateJournal(Journal journal, string id, string foundIn)
        {
            if (!Guid.TryParseExact(id, "N", out _) || journal == null || journal.Version != 1 || journal.Id != id ||
                journal.Directories == null || journal.Directories.Length == 0 || journal.Files == null || journal.Files.Count == 0)
                throw new IOException($"Invalid config recovery journal '{id}'.");
            if (!journal.Directories.Contains(foundIn, PathComparer) ||
                journal.Directories.Any(path => path == null || !PathComparer.Equals(path, NormalizeDirectory(path))) ||
                journal.Directories.Distinct(PathComparer).Count() != journal.Directories.Length)
                throw new IOException($"Invalid config recovery directories in '{id}'.");
            if (journal.Directories.Any(path => !_directories.Contains(path, PathComparer)))
                throw new IOException("Recover this config batch with all of its output directories: " + string.Join(", ", journal.Directories));
            var targets = new HashSet<string>(PathComparer);
            foreach (Entry file in journal.Files)
            {
                if (file == null || !journal.Directories.Contains(file.Directory, PathComparer) ||
                    !ValidHash(file.NewHash) || (file.OldHash != null && !ValidHash(file.OldHash)))
                    throw new IOException($"Invalid config recovery entry in '{id}'.");
                ValidateName(file.Name);
                string target = TargetPath(file);
                EnsureNoLinks(target);
                if (!targets.Add(target))
                    throw new IOException($"Duplicate config recovery target '{target}'.");
            }
        }

        private void Cleanup(Journal journal)
        {
            for (int i = 0; i < journal.Files.Count; i++)
            {
                foreach (string role in new[] { "old", "new", "restore" })
                    DeleteArtifact(PayloadPath(journal, i, role));
                _checkpoint?.Invoke($"cleanup:{i}");
            }
            // Remove the coordinator journal last, with terminal markers still present.
            foreach (string directory in journal.Directories.Reverse())
            {
                DeleteArtifact(JournalPath(directory, journal.Id) + ".tmp");
                DeleteArtifact(JournalPath(directory, journal.Id));
            }
            foreach (string marker in new[] { "ready", "committed", "rolledback" })
            {
                DeleteArtifact(MarkerPath(journal, marker) + ".tmp");
                DeleteArtifact(MarkerPath(journal, marker));
            }
        }

        private void CleanupOrphans()
        {
            string[] files = _directories.SelectMany(directory =>
                Directory.GetFiles(Path.Combine(directory, MetadataDirectory))).ToArray();
            // Payloads can only be created after every journal exists, and are removed before journals.
            // A surviving payload without a journal is evidence of damaged metadata, not disposable trash.
            foreach (string path in files)
            {
                string[] parts = Path.GetFileName(path).Split('.');
                if (parts.Length == 3 && Guid.TryParseExact(parts[0], "N", out _) &&
                    int.TryParse(parts[1], out _) && parts[2] is "old" or "new" or "restore")
                    throw new IOException($"Config recovery data has no journal: '{path}'. Keep it for manual recovery.");
            }
            foreach (string directory in _directories)
            {
                foreach (string path in Directory.GetFiles(Path.Combine(directory, MetadataDirectory)))
                {
                    string[] parts = Path.GetFileName(path).Split('.');
                    if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out _))
                        continue;
                    bool marker = parts[1] is "ready" or "committed" or "rolledback";
                    bool temporary = parts.Length == 3 && parts[2] == "tmp" && (marker || parts[1] == "json");
                    if ((marker && parts.Length == 2) || temporary)
                        DeleteArtifact(path);
                }
            }
        }

        private static bool HasMarker(Journal journal, string marker, byte[] manifest)
        {
            string path = MarkerPath(journal, marker);
            EnsureNoLinks(path);
            if (Directory.Exists(path))
                throw new IOException($"Invalid config transaction marker '{path}'.");
            if (!File.Exists(path))
                return false;
            if (File.ReadAllText(path, Encoding.ASCII) != Hash(manifest))
                throw new IOException($"Damaged config transaction marker '{path}'. Recovery was stopped.");
            return true;
        }

        private static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config output directory cannot be empty.");
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name == MetadataDirectory ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\'))
                throw new IOException($"Invalid config output filename '{name}'.");
        }

        private static string TargetPath(Entry file) => Path.Combine(file.Directory, file.Name);
        private static string JournalPath(string directory, string id) => Path.Combine(directory, MetadataDirectory, id + ".json");
        private static string MarkerPath(Journal journal, string marker) => Path.Combine(journal.Directories[0], MetadataDirectory, journal.Id + "." + marker);
        private static string PayloadPath(Journal journal, int index, string role) => Path.Combine(journal.Files[index].Directory,
            MetadataDirectory, $"{journal.Id}.{index}.{role}");
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        private static bool ValidHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

        private static string CurrentHash(string path)
        {
            EnsureNoLinks(path);
            if (Directory.Exists(path))
                throw new IOException($"Expected a config file, found directory '{path}'.");
            return File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
        }

        private static void WriteDurable(string path, byte[] bytes)
        {
            EnsureNoLinks(path);
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
            stream.Flush(true);
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            DeleteArtifact(path + ".tmp");
            WriteDurable(path + ".tmp", bytes);
            File.Move(path + ".tmp", path);
        }

        private static void ReplaceOrMove(string source, string target)
        {
            EnsureNoLinks(target);
            if (File.Exists(target))
                File.Replace(source, target, null);
            else
                File.Move(source, target);
        }

        private static void DeleteArtifact(string path)
        {
            EnsureNoLinks(path);
            File.Delete(path);
        }

        private static void EnsureNoLinks(string path)
        {
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException($"Config transaction paths cannot contain symbolic links or junctions: '{current}'.");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (int i = _locks.Count - 1; i >= 0; i--)
                _locks[i].Dispose();
            _locks.Clear();
        }
    }
}
