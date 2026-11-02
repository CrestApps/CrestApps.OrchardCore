using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Protocol;

namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// Builds the compact JSON the NLWeb AI steps send to the model, so prompts stay small however large the content is.
/// </summary>
internal static class NLWebAIPayloads
{
    /// <summary>
    /// The longest text of one result sent to the model.
    /// </summary>
    internal const int MaxItemLength = 1500;

    /// <summary>
    /// Describes the results for the model: their position, type, name, URL and a shortened copy of their
    /// schema.org object.
    /// </summary>
    /// <param name="results">The results.</param>
    /// <returns>The JSON text.</returns>
    public static string DescribeResults(IEnumerable<NLWebResult> results)
    {
        var array = new JsonArray();
        var index = 0;

        foreach (var result in results)
        {
            var details = result.Item?.ToJsonString(NLWebJson.SerializerOptions) ?? string.Empty;

            if (details.Length > MaxItemLength)
            {
                details = details.Substring(0, MaxItemLength);
            }

            array.Add(new JsonObject
            {
                ["index"] = index++,
                ["type"] = result.Item?["@type"]?.DeepClone(),
                ["name"] = result.Name,
                ["url"] = result.Url,
                ["data"] = details,
            });
        }

        return array.ToJsonString(NLWebJson.SerializerOptions);
    }
}
