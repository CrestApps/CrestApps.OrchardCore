using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Lists AI test runs of a batch or of a test case, as the Runs page of the AI tests does, with each run's outcome and
/// the judge's findings, the pass rate and how many runs are still pending.
/// </summary>
public sealed class ListAITestRunsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAITestRuns";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "batchId": {
          "type": "string",
          "description": "The batch runAITests returned. Takes precedence over testCaseId."
        },
        "testCaseId": {
          "type": "string",
          "description": "Only the runs of this test case, newest first, from listAITestCases."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many runs per page, at most 50. Defaults to 20."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists test runs of a batch or a test case with status, score, pass or fail, the judge's findings, the pass rate and how many are still pending. Use it after runAITests.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIQuality))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("batchId", out var batchId);
        arguments.TryGetFirstString("testCaseId", out var testCaseId);
        batchId = QualityToolSupport.Normalize(batchId);
        testCaseId = QualityToolSupport.Normalize(testCaseId);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var runStore = services.GetRequiredService<IAITestRunStore>();
        IReadOnlyList<AITestRun> runs;
        IReadOnlyList<AITestRun> all;
        int total;

        if (batchId is not null)
        {
            // The pass rate and pending count are over the whole batch, as on the Runs page.
            all = await runStore.ListByBatchAsync(batchId, cancellationToken);
            total = all.Count;
            runs = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            if (total == 0)
            {
                return $"No test runs belong to the batch '{batchId}'. runAITests returns the batch id; runs show up as soon as they are queued.";
            }
        }
        else
        {
            if (testCaseId is not null && await services.GetRequiredService<IAITestCaseStore>().FindByIdAsync(testCaseId, cancellationToken) is null)
            {
                return $"No test case has the ID '{testCaseId}'. Use listAITestCases to find it.";
            }

            (runs, total) = await runStore.PageAsync(testCaseId, page, pageSize, cancellationToken);
            all = runs;
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var run in runs)
        {
            items.Add(new
            {
                id = run.ItemId,
                testCaseId = run.TestCaseId,
                testCaseName = run.TestCaseName,
                profileId = run.ProfileId,
                profileVersion = run.ProfileVersion,
                batchId = run.BatchId,
                status = run.Status.ToString(),
                passed = run.Status switch
                {
                    AITestRunStatus.Passed => true,
                    AITestRunStatus.Failed => false,
                    _ => (bool?)null,
                },
                score = run.Score,
                summary = run.Summary,
                error = run.Error,
                criteria = (run.Criteria ?? []).Select(criterion => new
                {
                    name = criterion.Name,
                    met = criterion.Met,
                    reason = criterion.Reason,
                }).ToList(),
                requestedBy = run.RequestedBy,
                queuedAt = await AgentToolResults.ToLocalAsync(localClock, run.QueuedUtc),
                completedAt = await AgentToolResults.ToLocalAsync(localClock, run.CompletedUtc),
                detailsPath = QualityToolSupport.GetTestRunPath(services, run.ItemId),
            });
        }

        return AgentToolResults.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
            passRate = AITestRunner.PassRate(all),
            pending = all.Count(run => run.Status is AITestRunStatus.Queued or AITestRunStatus.Running),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
