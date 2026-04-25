using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

internal static class AIPropertiesMergeHelper
{
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
