using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Agent.ApiKeys;

/// <summary>
/// What the API key tools share: the admin links and the check whether a key works now.
/// </summary>
internal static class AIApiKeyToolSupport
{
    /// <summary>
    /// The controller of the API keys page.
    /// </summary>
    private const string Controller = "AIApiKeys";

    /// <summary>
    /// Gets the address of the page that makes a new key, where its value is shown once.
    /// </summary>
    public static string GetCreatePath(IServiceProvider services)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, Controller, "Create");

    /// <summary>
    /// Gets the address of a key's editor.
    /// </summary>
    public static string GetEditPath(IServiceProvider services, string keyId)
        => AgentToolLinks.GetAdminPath(services, AIConstants.Feature.Area, Controller, "Edit", new RouteValueDictionary { ["id"] = keyId });

    /// <summary>
    /// Whether a key is accepted at a moment: it is enabled and has not expired.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="utcNow">The moment, in UTC.</param>
    /// <returns><see langword="true"/> when a client can use the key.</returns>
    public static bool Works(AIApiKey key, DateTime utcNow)
        => key.Enabled && (key.ExpiresUtc is null || key.ExpiresUtc > utcNow);
}
