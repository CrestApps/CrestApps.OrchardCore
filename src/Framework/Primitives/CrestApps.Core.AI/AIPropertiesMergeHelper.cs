using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI;

/// <summary>
/// After a JSON merge with <c>MergeArrayHandling.Replace</c>, this helper
/// post-processes known settings types to merge their named entries by name
/// (upsert) instead of fully replacing the arrays.
///
/// Existing entries not in the incoming data are preserved; incoming entries
/// with the same name as existing entries replace them; new incoming entries
/// are appended.
/// </summary>
internal static class AIPropertiesMergeHelper
{
    public static void MergeNamedEntries(JsonObject mergedContainer, JsonObject existingSnapshot)
    {
        if (mergedContainer is null || existingSnapshot is null)
        {
            return;
        }

        MergeByName<AIProfilePostSessionSettings, PostSessionTask>(
            mergedContainer, existingSnapshot,
            s => s.PostSessionTasks, (s, list) => s.PostSessionTasks = list,
            e => e.Name);

        MergeByName<AIProfileDataExtractionSettings, DataExtractionEntry>(
            mergedContainer, existingSnapshot,
            s => s.DataExtractionEntries, (s, list) => s.DataExtractionEntries = list,
            e => e.Name);

        MergeByName<AnalyticsMetadata, ConversionGoal>(
            mergedContainer, existingSnapshot,
            s => s.ConversionGoals, (s, list) => s.ConversionGoals = list,
            e => e.Name);
    }

    private static void MergeByName<TSettings, TEntry>(
        JsonObject mergedContainer,
        JsonObject existingSnapshot,
        Func<TSettings, List<TEntry>> getEntries,
        Action<TSettings, List<TEntry>> setEntries,
        Func<TEntry, string> getName)
        where TSettings : class, new()
    {
        var typeName = typeof(TSettings).Name;

        var existingNode = existingSnapshot[typeName];

        if (existingNode is null)
        {
            return;
        }

        var existingSettings = existingNode.Deserialize<TSettings>(JSOptions.CaseInsensitive);

        if (existingSettings is null)
        {
            return;
        }

        var existingEntries = getEntries(existingSettings);

        if (existingEntries.Count == 0)
        {
            return;
        }

        var mergedNode = mergedContainer[typeName];
        var mergedSettings = mergedNode?.Deserialize<TSettings>(JSOptions.CaseInsensitive) ?? new TSettings();
        var incomingEntries = getEntries(mergedSettings);

        var result = new List<TEntry>(existingEntries);

        foreach (var incoming in incomingEntries)
        {
            var name = getName(incoming);

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var existingIndex = result.FindIndex(e =>
            string.Equals(getName(e), name, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                result[existingIndex] = incoming;
            }
            else
            {
                result.Add(incoming);
            }
        }

        setEntries(mergedSettings, result);
        mergedContainer[typeName] = JsonSerializer.SerializeToNode(mergedSettings, JSOptions.CaseInsensitive);
    }
}
