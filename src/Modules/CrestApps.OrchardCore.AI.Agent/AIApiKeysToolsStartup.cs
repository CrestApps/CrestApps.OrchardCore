using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ApiKeys;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, change and delete API keys, when the AI API is on. A key's value never passes
/// through the assistant, so keys are made on the API keys page only.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class AIApiKeysToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIApiKeysToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIApiKeysToolsStartup(IStringLocalizer<AIApiKeysToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIApiKeysTool>(ListAIApiKeysTool.TheName)
            .WithTitle(S["List AI API Keys"])
            .WithDescription(S["Lists API keys with name, kind, prefix, scopes, roles, allowed origins and profiles, expiry, enabled state and last use. The key value and its hash are never returned."])
            .WithCategory(S["AI API Keys"])
            .Selectable();

        services.AddCoreAITool<SaveAIApiKeyTool>(SaveAIApiKeyTool.TheName)
            .WithTitle(S["Save AI API Key"])
            .WithDescription(S["Updates an existing API key: enable or disable it, or change its scopes, roles, origins, allowed profiles or expiry. It cannot create a key, because a new key's value is shown once and must not pass through the assistant; send the user to the API keys page for that."])
            .WithCategory(S["AI API Keys"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIApiKeyTool>(DeleteAIApiKeyTool.TheName)
            .WithTitle(S["Delete AI API Keys"])
            .WithDescription(S["Deletes (revokes) API keys by id. Clients using them stop working at once."])
            .WithCategory(S["AI API Keys"])
            .RequireApproval()
            .Selectable();
    }
}
