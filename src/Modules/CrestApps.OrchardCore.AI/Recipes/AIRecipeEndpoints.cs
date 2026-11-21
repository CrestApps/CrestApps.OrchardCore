using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// The endpoints a recipe's AI connection or deployment would have the site call: every string property, at any depth,
/// whose name ends with <c>Endpoint</c> (a provider endpoint, an OAuth2 token endpoint), and for a tool instance also
/// <c>Url</c> (an HTTP API Request base URL, a documentation site, sitemap or index address). A recipe step checks them with
/// <see cref="IAIEndpointPolicy"/>, as the editor does, so a recipe cannot point a connection at the site's own network.
/// </summary>
internal static class AIRecipeEndpoints
{
    /// <summary>
    /// Returns the first endpoint the policy refuses, or <see langword="null"/>.
    /// </summary>
    /// <param name="entry">The connection or deployment, as the recipe filled it in.</param>
    /// <param name="policy">The endpoint policy.</param>
    /// <param name="includeUrls">Whether properties whose name ends with <c>Url</c> are addresses too.</param>
    public static string FindRefused(object entry, IAIEndpointPolicy policy, bool includeUrls = false)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (entry is null)
        {
            return null;
        }

        return Endpoints(JsonSerializer.SerializeToNode(entry, entry.GetType()), includeUrls).FirstOrDefault(url => !policy.IsAllowed(url));
    }

    private static bool IsAddressName(string name, bool includeUrls)
        => name.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase) ||
            (includeUrls && name.EndsWith("Url", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Endpoints(JsonNode node, bool includeUrls)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, value) in obj)
            {
                if (value is JsonValue leaf &&
                    IsAddressName(name, includeUrls) &&
                    leaf.TryGetValue<string>(out var url) &&
                    !string.IsNullOrWhiteSpace(url))
                {
                    yield return url;
                }
                else
                {
                    foreach (var nested in Endpoints(value, includeUrls))
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
                foreach (var nested in Endpoints(item, includeUrls))
                {
                    yield return nested;
                }
            }
        }
    }
}
