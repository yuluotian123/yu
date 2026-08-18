using System.Text.Json.Nodes;

namespace GameLogic
{
    public interface ISaveSection
    {
        string SectionKey { get; }
        string EntryKey { get; }
        int SchemaVersion { get; }
        JsonObject Capture();
        void Restore(JsonObject state, int schemaVersion);
    }
}
