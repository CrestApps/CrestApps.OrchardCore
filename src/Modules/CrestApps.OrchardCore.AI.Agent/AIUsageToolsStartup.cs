using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Usage;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that reports AI usage and estimated cost, when AI usage analytics is on. It reads the same usage
/// records and deployment prices as the AI Usage Analytics report.
/// </summary>
[RequireFeatures(AIConstants.Feature.UsageAnalytics)]
public sealed class AIUsageToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIUsageToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIUsageToolsStartup(IStringLocalizer<AIUsageToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<QueryAIUsageTool>(QueryAIUsageTool.TheName)
            .WithTitle(S["Query AI Usage"])
            .WithDescription(S["Returns AI usage for a period: requests, input and output tokens, audio and image usage and estimated cost, grouped by model, deployment, profile, connection, user or day. Costs are estimates from the deployments' prices."])
            .WithCategory(S["AI Analytics"])
            .Selectable();
    }
}
