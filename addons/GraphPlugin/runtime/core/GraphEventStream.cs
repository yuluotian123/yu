using System;
using System.Collections.Generic;

// Scoped to an execution context; related event flows explicitly share the stream.
public sealed class GraphEventStream
{
    private readonly Dictionary<string, ulong> _versions = new(StringComparer.Ordinal);
    public void Publish(string name)
    {
        if (!string.IsNullOrWhiteSpace(name)) _versions[name] = GetVersion(name) + 1;
    }
    public ulong GetVersion(string name) =>
        !string.IsNullOrWhiteSpace(name) && _versions.TryGetValue(name, out var value) ? value : 0;
}
