using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Quality;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers, when the AI Quality feature is on, the tool that reads AI test runs, the action that turns a real
/// conversation into a test case, and the record kinds <c>aiAlertRule</c>, <c>aiQACohort</c> and <c>aiTestCase</c>,
/// which the generic record tools list, read, save and delete by replaying the admin editors of those records. The
/// earlier quality tools stay in <see cref="QualityToolsStartup"/>.
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

        services.AddCoreAITool<CreateAITestCaseFromConversationTool>(CreateAITestCaseFromConversationTool.TheName)
            .WithTitle(S["Create AI Test Case From Conversation"])
            .WithDescription(S["Turns a real conversation into a simulation test case, so a conversation that went wrong can be replayed until the AI handles it: the customer's lines become the scenario and the conversation's profile and variables are kept. Change the test later with saveRecord (kind aiTestCase); saving runs nothing, so run it with runAITests."])
            .WithCategory(S["AI Quality"])
            .Selectable();

        // The alert rules, QA cohorts and test cases are kept in stores, not behind a catalog manager, so every hook
        // does what the record's admin controller does with its store. The admin editors only offer existing profiles
        // in a list, so the checks add that the profiles exist.
        services.AddAIRecordKind<AIAlertRule>("aiAlertRule", kind => kind
            .WithTitle(S["AI alert rule"])
            .WithDescription(S["an AI alert rule: a conversation metric (QA failure rate, resolution rate, negative feedback rate, tool failure rate, average QA score, handle time, response latency or the number of conversations) of one AI profile or all of them, measured over a time window and compared with a threshold; when it crosses, the rule opens and emails the addresses in NotifyEmails (separated by commas), and it closes when the metric is back. Rates are percentages from 0 to 100. Use listAIAlerts for which rules are open now and their incidents"])
            .WithPermission(AIPermissions.ManageAIQuality)
            .FindUsing(AIQualityRecordKinds.FindAsync<IAIAlertRuleStore, AIAlertRule>)
            .NewUsing((serviceProvider, _) => AIQualityRecordKinds.NewAlertRuleAsync(serviceProvider))
            .ListUsing(AIQualityRecordKinds.ListAsync<IAIAlertRuleStore, AIAlertRule>)
            .ValidateUsing(AIQualityRecordKinds.ValidateAlertRuleAsync)
            .SaveUsing(AIQualityRecordKinds.SaveAsync<IAIAlertRuleStore, AIAlertRule>)
            .DeleteUsing(AIQualityRecordKinds.DeleteAsync<IAIAlertRuleStore, AIAlertRule>)
            .WithProjection(rule => new
            {
                id = rule.ItemId,
                name = rule.Name,
                enabled = rule.Enabled,
                metric = rule.Metric.ToString(),
                profileId = rule.ProfileId,
                condition = rule.Condition.ToString(),
                threshold = rule.Threshold,
                windowMinutes = rule.WindowMinutes,
                minimumSampleSize = rule.MinimumSampleSize,
                checkEveryMinutes = rule.CheckEveryMinutes,
                notifyEmails = AIQualityRecordKinds.SplitEmails(rule.NotifyEmails),
                isOpen = rule.IsOpen,
                lastValue = rule.LastValue,
                lastSampleSize = rule.LastSampleSize,
                incidentCount = rule.Incidents?.Count ?? 0,
            })
            .UseEditorReplay());

        services.AddAIRecordKind<AIQACohort>("aiQACohort", kind => kind
            .WithTitle(S["AI QA cohort"])
            .WithDescription(S["an AI QA cohort: a share (SamplePercent, 1 to 100) of the real conversations of the AI profiles in ProfileIds, each scored after it closes by a judge model against the criteria (one per line), with the passing score and whether the judge also checks the answers against the profile's knowledge. A new cohort scores the conversations that close from then on. While Enabled, it makes one paid judge model call per sampled conversation; set Enabled to false to stop the cost. Read the scores with summarizeAIQuality"])
            .WithPermission(AIPermissions.ManageAIQuality)
            .FindUsing(AIQualityRecordKinds.FindAsync<IAIQACohortStore, AIQACohort>)
            .NewUsing((serviceProvider, _) => AIQualityRecordKinds.NewCohortAsync(serviceProvider))
            .ListUsing(AIQualityRecordKinds.ListAsync<IAIQACohortStore, AIQACohort>)
            .ValidateUsing(AIQualityRecordKinds.ValidateCohortAsync)
            .SaveUsing(AIQualityRecordKinds.SaveAsync<IAIQACohortStore, AIQACohort>)
            .DeleteUsing(AIQualityRecordKinds.DeleteAsync<IAIQACohortStore, AIQACohort>)
            .WithProjection(cohort => new
            {
                id = cohort.ItemId,
                name = cohort.Name,
                label = cohort.GetLabel(),
                enabled = cohort.Enabled,
                profileIds = cohort.ProfileIds?.ToList() ?? [],
                samplePercent = cohort.SamplePercent,
                criteria = cohort.Criteria,
                passingScore = cohort.PassingScore,
                checkKnowledge = cohort.CheckKnowledge,
                judgeDeploymentName = cohort.JudgeDeploymentName,
            })
            .UseEditorReplay());

        services.AddAIRecordKind<AITestCase>("aiTestCase", kind => kind
            .WithTitle(S["AI test case"])
            .WithDescription(S["an AI simulation test case: the AI profile under test (and optionally one of its saved versions), the simulated customer's scenario, what the conversation must achieve (Criteria, one per line), the turn limit, the passing score and the customer and judge deployments. Variables and MockedTools are written as 'name: value' lines, one per line; a mocked tool returns its value instead of running, so a test changes nothing. Saving runs nothing: run it with runAITests and read the outcome with listAITestRuns. createAITestCaseFromConversation starts a test case from a real conversation"])
            .WithPermission(AIPermissions.ManageAIQuality)
            .FindUsing(AIQualityRecordKinds.FindAsync<IAITestCaseStore, AITestCase>)
            .NewUsing((serviceProvider, _) => AIQualityRecordKinds.NewTestCaseAsync(serviceProvider))
            .ListUsing(AIQualityRecordKinds.ListAsync<IAITestCaseStore, AITestCase>)
            .ValidateUsing(AIQualityRecordKinds.ValidateTestCaseAsync)
            .SaveUsing(AIQualityRecordKinds.SaveTestCaseAsync)
            .DeleteUsing(AIQualityRecordKinds.DeleteAsync<IAITestCaseStore, AITestCase>)
            .WithProjection(testCase => new
            {
                id = testCase.ItemId,
                name = testCase.Name,
                profileId = testCase.ProfileId,
                profileVersion = testCase.ProfileVersion,
                scenario = testCase.Scenario,
                criteria = testCase.Criteria,
                variables = testCase.Variables is { Count: > 0 }
                    ? new Dictionary<string, string>(testCase.Variables, StringComparer.OrdinalIgnoreCase)
                    : null,

                // The mocked results can be long; the names say which tools are mocked.
                mockedTools = testCase.MockedTools?.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
                maxTurns = testCase.MaxTurns,
                passingScore = testCase.PassingScore,
                customerDeploymentName = testCase.CustomerDeploymentName,
                judgeDeploymentName = testCase.JudgeDeploymentName,
                fromConversationId = testCase.SourceSessionId,
                lastStatus = testCase.LastStatus?.ToString(),
            })
            .UseEditorReplay());
    }
}
