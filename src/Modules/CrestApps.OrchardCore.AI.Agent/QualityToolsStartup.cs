using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Gives the admin AI agent tools over QA results, test cases and alerts when the AI Quality feature is on, so an admin
/// can ask why conversations failed and what to change.
/// </summary>
[RequireFeatures(AIConstants.Feature.Quality)]
public sealed class QualityToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualityToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QualityToolsStartup(IStringLocalizer<QualityToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<SummarizeAIQualityTool>(SummarizeAIQualityTool.TheName)
            .WithTitle(S["Summarize AI Quality"])
            .WithDescription(S["Sums up QA scores, issues and common questions of AI conversations over a period."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<ListAITestCasesTool>(ListAITestCasesTool.TheName)
            .WithTitle(S["List AI Test Cases"])
            .WithDescription(S["Lists the AI simulation test cases."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<RunAITestsTool>(RunAITestsTool.TheName)
            .WithTitle(S["Run AI Tests"])
            .WithDescription(S["Queues AI simulation test cases to run."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<ListAIAlertsTool>(ListAIAlertsTool.TheName)
            .WithTitle(S["List AI Alerts"])
            .WithDescription(S["Lists the AI alert rules and which are open."])
            .WithCategory(S["AI Quality"])
            .Selectable();
    }
}
