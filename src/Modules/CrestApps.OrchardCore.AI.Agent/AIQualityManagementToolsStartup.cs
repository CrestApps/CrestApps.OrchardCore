using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that read AI test runs and manage test cases, alert rules and QA cohorts, when the AI Quality
/// feature is on. The existing quality tools stay in <see cref="QualityToolsStartup"/>.
/// </summary>
[RequireFeatures(AIConstants.Feature.Quality)]
public sealed class AIQualityManagementToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIQualityManagementToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIQualityManagementToolsStartup(IStringLocalizer<AIQualityManagementToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAITestRunsTool>(ListAITestRunsTool.TheName)
            .WithTitle(S["List AI Test Runs"])
            .WithDescription(S["Lists test runs of a batch or a test case with status, score, pass or fail, the judge's findings, the pass rate and how many are still pending. Use it after runAITests."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<SaveAITestCaseTool>(SaveAITestCaseTool.TheName)
            .WithTitle(S["Save AI Test Case"])
            .WithDescription(S["Creates or updates a simulation test case: profile, optional profile version, customer scenario, pass criteria, variables, mocked tool results, turn limit, passing score and the customer and judge deployments. Pass fromConversationId to start from a real conversation."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<DeleteAITestCasesTool>(DeleteAITestCasesTool.TheName)
            .WithTitle(S["Delete AI Test Cases"])
            .WithDescription(S["Deletes test cases by id."])
            .WithCategory(S["AI Quality"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SaveAIAlertRuleTool>(SaveAIAlertRuleTool.TheName)
            .WithTitle(S["Save AI Alert Rule"])
            .WithDescription(S["Creates or updates an alert rule that watches a conversation metric over a time window and emails the listed addresses when it crosses a threshold. Can also turn a rule on or off."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<DeleteAIAlertRuleTool>(DeleteAIAlertRuleTool.TheName)
            .WithTitle(S["Delete AI Alert Rules"])
            .WithDescription(S["Deletes alert rules by id, with their incident history."])
            .WithCategory(S["AI Quality"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListAIQACohortsTool>(ListAIQACohortsTool.TheName)
            .WithTitle(S["List AI QA Cohorts"])
            .WithDescription(S["Lists QA cohorts: profiles covered, sample percent, criteria, passing score, judge deployment and whether each is on."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        services.AddCoreAITool<SaveAIQACohortTool>(SaveAIQACohortTool.TheName)
            .WithTitle(S["Save AI QA Cohort"])
            .WithDescription(S["Creates or updates a QA cohort that scores a sample of real conversations with a judge model. An enabled cohort makes paid model calls for every sampled conversation until it is turned off."])
            .WithCategory(S["AI Quality"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIQACohortTool>(DeleteAIQACohortTool.TheName)
            .WithTitle(S["Delete AI QA Cohorts"])
            .WithDescription(S["Deletes QA cohorts by id. Scores already recorded stay."])
            .WithCategory(S["AI Quality"])
            .RequireApproval()
            .Selectable();
    }
}
