using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

internal static class AIPropertiesMergeHelper
{
    /// <summary>
    /// Recursively merges named entries from <paramref name="existing"/> into <paramref name="current"/>.
    /// Arrays of objects that contain a <c>Name</c> property are merged by name, preserving
    /// entries from <paramref name="current"/> and adding missing ones from <paramref name="existing"/>.
    /// </summary>
    /// <param name="current">The current JSON object to merge into.</param>
    /// <param name="existing">The existing JSON object whose entries are merged when absent from <paramref name="current"/>.</param>
    public static void MergeNamedEntries(JsonObject current, JsonObject existing)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(existing);

        foreach (var (key, currentNode) in current)
        {
            if (!existing.TryGetPropertyValue(key, out var existingNode) ||
                currentNode is null ||
                existingNode is null)
            {
                continue;
            }

            if (currentNode is JsonObject currentObject && existingNode is JsonObject existingObject)
            {
                MergeNamedEntries(currentObject, existingObject);
                continue;
            }

            if (currentNode is not JsonArray currentArray || existingNode is not JsonArray existingArray)
            {
                continue;
            }

            if (!TryCreateNamedMap(currentArray, out var currentEntries) ||
                !TryCreateNamedMap(existingArray, out var existingEntries))
            {
                continue;
            }

            foreach (var (name, existingEntry) in existingEntries)
            {
                currentEntries.TryAdd(name, existingEntry);
            }

            current[key] = new JsonArray(currentEntries.Values.Select(node => node.DeepClone()).ToArray());
        }
    }

    private static bool TryCreateNamedMap(JsonArray array, out Dictionary<string, JsonNode> entries)
    {
        entries = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in array)
        {
            if (node is not JsonObject item || TryGetName(item) is not { Length: > 0 } name)
            {
                entries = null;
                return false;
            }

            entries[name] = item;
        }

        return true;
    }

    private static string TryGetName(JsonObject item)
    {
        if (item.TryGetPropertyValue("Name", out var nameNode) ||
            item.TryGetPropertyValue("name", out nameNode))
        {
            return nameNode?.GetValue<string>()?.Trim();
        }

        return null;
    }
}
