using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AgenticCommerce.Deployments;

internal sealed class AgentPlatformsDeploymentSource : CatalogDeploymentSource<AgentPlatformsDeploymentStep, AgentPlatform>
{
    public AgentPlatformsDeploymentSource(IAgentPlatformManager manager)
        : base("Platforms", manager)
    {
    }

    // A platform's API key and signing secret stay on this site: the importing site gives the platform new ones.
    protected override JsonObject Export(AgentPlatform entry)
    {
        var json = base.Export(entry);

        foreach (var key in json.Select(property => property.Key).Where(IsCredential).ToList())
        {
            json.Remove(key);
        }

        return json;
    }

    private static bool IsCredential(string key)
        => string.Equals(key, nameof(AgentPlatform.ApiKeyHash), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, nameof(AgentPlatform.ApiKeyHint), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, nameof(AgentPlatform.ProtectedWebhookSecret), StringComparison.OrdinalIgnoreCase);
}
