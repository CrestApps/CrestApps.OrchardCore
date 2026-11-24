using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that lists AI provider connections, when connection management is on. Keys are never shown.
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
    }
}
