using System.Collections.Generic;
using System.Linq;

public static class StateTagUtility
{
    public static IReadOnlyList<string> ParseTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return System.Array.Empty<string>();

        return tags
            .Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(System.StringComparer.Ordinal)
            .ToList();
    }

    public static bool ContainsTag(string tags, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        foreach (string parsedTag in ParseTags(tags))
        {
            if (string.Equals(parsedTag, tag, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
