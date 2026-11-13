using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// The endpoints a recipe's AI connection or deployment would have the site call: every string property, at any depth,
/// whose name ends with <c>Endpoint</c> (a provider endpoint, an OAuth2 token endpoint). A recipe step checks them with
/// <see cref="IAIEndpointPolicy"/>, as the editor does, so a recipe cannot point a connection at the site's own network.
/// </summary>
internal static class AIRecipeEndpoints
{
    /// <summary>
    /// Returns the first endpoint the policy refuses, or <see langword="null"/>.
    /// </summary>
    /// <param name="entry">The connection or deployment, as the recipe filled it in.</param>
    /// <param name="policy">The endpoint policy.</param>
    public static string FindRefused(object entry, IAIEndpointPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (entry is null)
        {
            return null;
        }

        return Endpoints(JsonSerializer.SerializeToNode(entry, entry.GetType())).FirstOrDefault(url => !policy.IsAllowed(url));
    }

    private static IEnumerable<string> Endpoints(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, value) in obj)
            {
                if (value is JsonValue leaf &&
                    name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) &&
                    leaf.TryGetValue<string>(out var url) &&
                    !string.IsNullOrWhiteSpace(url))
                {
                    yield return url;
                }
                else
                {
                    foreach (var nested in Endpoints(value))
                    {
                        yield return nested;
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                foreach (var nested in Endpoints(item))
                {
                    yield return nested;
                }
            }
        }
    }
}
