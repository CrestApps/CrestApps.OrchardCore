using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete AI provider connections, when connection management is on. Keys are
/// never taken or shown, and the connection the running assistant talks through cannot be changed from a chat.
/// </summary>
[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class AIProviderConnectionsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProviderConnectionsToolsStartup(IStringLocalizer<AIProviderConnectionsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIProviderConnectionsTool>(ListAIProviderConnectionsTool.TheName)
            .WithTitle(S["List AI Provider Connections"])
            .WithDescription(S["Lists provider connections with provider, name, title, endpoint, authentication type, whether a key is stored, whether the connection comes from configuration (read-only) and which deployments use it. With includeProviders it lists the providers a connection can be created for."])
            .WithCategory(S["AI Models"])
            .Selectable();

        services.AddCoreAITool<SaveAIProviderConnectionTool>(SaveAIProviderConnectionTool.TheName)
            .WithTitle(S["Save AI Provider Connection"])
            .WithDescription(S["Creates a provider connection or updates one by id. Keys are never taken or shown: a person enters the key on the admin page the result links to. Saving reloads the site, which can end the current chat, so do it last. Connections from configuration cannot be changed."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIProviderConnectionTool>(DeleteAIProviderConnectionTool.TheName)
            .WithTitle(S["Delete AI Provider Connections"])
            .WithDescription(S["Deletes provider connections by id and reloads the site, which can end the current chat. Deployments that used a deleted connection stop working. Connections from configuration are skipped."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();
    }
}
