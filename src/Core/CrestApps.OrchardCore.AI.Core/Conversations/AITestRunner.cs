using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Queues and runs AI test cases: the profile talks to the simulated customer with the case's variables and mocked
/// tools, and the judge scores the conversation against the case's criteria.
/// </summary>
/// <remarks>
/// Runs are queued as records and run by a background task, because one run is several model round trips and a
/// batch of them would outlast any request. Each run keeps its conversation and verdict, so a batch's pass rate
/// across repeats can be read from its runs.
/// </remarks>
public sealed class AITestRunner
{
    /// <summary>
    /// The most runs one request may queue.
    /// </summary>
    public const int MaxRunsPerBatch = 500;

    private readonly IAITestCaseStore _testCaseStore;
    private readonly IAITestRunStore _runStore;
    private readonly IAIProfileManager _profileManager;
    private readonly AIProfileVersionCopies _versionCopies;
    private readonly AIConversationSimulator _simulator;
    private readonly AIConversationEvaluationService _evaluationService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AITestRunner"/> class.
    /// </summary>
    /// <param name="testCaseStore">The test case store.</param>
    /// <param name="runStore">The test run store.</param>
    /// <param name="profileManager">The AI profile manager.</param>
    /// <param name="versionCopies">Runs an earlier version of a profile.</param>
    /// <param name="simulator">Plays the simulated customer against the profile.</param>
    /// <param name="evaluationService">Judges the conversation.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public AITestRunner(
        IAITestCaseStore testCaseStore,
        IAITestRunStore runStore,
        IAIProfileManager profileManager,
        AIProfileVersionCopies versionCopies,
        AIConversationSimulator simulator,
        AIConversationEvaluationService evaluationService,
        IClock clock,
        ILogger<AITestRunner> logger)
    {
        _testCaseStore = testCaseStore;
        _runStore = runStore;
        _profileManager = profileManager;
        _versionCopies = versionCopies;
        _simulator = simulator;
        _evaluationService = evaluationService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Queues a batch: each test, the given number of times.
    /// </summary>
    /// <param name="testCases">The tests to run.</param>
    /// <param name="repeats">How many times to run each, 1 to 20.</param>
    /// <param name="requestedBy">Who asked.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batch identifier and the queued runs.</returns>
    public async Task<(string BatchId, IReadOnlyList<AITestRun> Runs)> QueueAsync(
        IEnumerable<AITestCase> testCases,
        int repeats,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testCases);

        var batchId = UniqueId.GenerateId();
        var runs = new List<AITestRun>();
        var times = Math.Clamp(repeats, 1, 20);

        foreach (var testCase in testCases.Where(testCase => testCase is not null))
        {
            for (var i = 0; i < times && runs.Count < MaxRunsPerBatch; i++)
            {
                var run = new AITestRun
                {
                    ItemId = UniqueId.GenerateId(),
                    TestCaseId = testCase.ItemId,
                    TestCaseName = testCase.Name,
                    ProfileId = testCase.ProfileId,
                    BatchId = batchId,
                    Status = AITestRunStatus.Queued,
                    QueuedUtc = _clock.UtcNow,
                    RequestedBy = requestedBy,
                };

                await _runStore.CreateAsync(run, cancellationToken);
                runs.Add(run);
            }
        }

        return (batchId, runs);
    }

    /// <summary>
    /// Runs a queued run and records its outcome on the run and on its test.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The run, finished.</returns>
    public async Task<AITestRun> RunAsync(AITestRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var testCase = await _testCaseStore.FindByIdAsync(run.TestCaseId, cancellationToken);

        try
        {
            if (testCase is null)
            {
                return await FinishAsync(run, null, AITestRunStatus.Error, "The test case no longer exists.", cancellationToken);
            }

            var profile = testCase.ProfileVersion is > 0
                ? await _versionCopies.GetOrCreateAsync(testCase.ProfileId, testCase.ProfileVersion.Value, cancellationToken)
                : await _profileManager.FindByIdAsync(testCase.ProfileId, cancellationToken);

            if (profile is null)
            {
                return await FinishAsync(run, testCase, AITestRunStatus.Error, "The AI profile or the version under test was not found.", cancellationToken);
            }

            run.ProfileVersion = testCase.ProfileVersion ?? (profile.TryGet<AIProfileVersionMetadata>(out var version) ? version.Version : null);

            var simulation = await _simulator.SimulateAsync(new AIConversationSimulationRequest
            {
                Profile = profile,
                Scenario = testCase.Scenario,
                Variables = testCase.Variables,
                MockedTools = testCase.MockedTools,
                MaxTurns = testCase.MaxTurns,
                CustomerDeploymentName = testCase.CustomerDeploymentName,
            }, cancellationToken);

            run.Turns = simulation.Turns.ToList();

            var evaluation = await _evaluationService.EvaluateTurnsAsync(
                profile,
                session: null,
                simulation.Turns,
                AIConversationEvaluator.ParseCriteria(testCase.Criteria),
                testCase.PassingScore,
                label: $"Test: {testCase.Name}",
                simulation.Instructions,
                testCase.JudgeDeploymentName,
                cancellationToken);

            if (evaluation is null)
            {
                return await FinishAsync(run, testCase, AITestRunStatus.Error, "The conversation could not be judged: no deployment can judge it, or the customer never spoke.", cancellationToken);
            }

            run.Score = evaluation.Score;
            run.Summary = evaluation.Summary;
            run.Criteria = evaluation.Criteria;
            run.EvaluationId = evaluation.ItemId;

            return await FinishAsync(run, testCase, evaluation.Passed ? AITestRunStatus.Passed : AITestRunStatus.Failed, null, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI test run '{RunId}' could not finish.", run.ItemId.SanitizeLogValue());

            return await FinishAsync(run, testCase, AITestRunStatus.Error, ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// The pass rate of a set of runs: the share of finished runs that passed.
    /// </summary>
    /// <param name="runs">The runs.</param>
    /// <returns>The rate from 0 to 1, or <see langword="null"/> when none has finished.</returns>
    public static double? PassRate(IEnumerable<AITestRun> runs)
    {
        var finished = (runs ?? []).Where(run => run.Status is AITestRunStatus.Passed or AITestRunStatus.Failed).ToList();

        return finished.Count == 0
            ? null
            : (double)finished.Count(run => run.Status == AITestRunStatus.Passed) / finished.Count;
    }

    private async Task<AITestRun> FinishAsync(AITestRun run, AITestCase testCase, AITestRunStatus status, string error, CancellationToken cancellationToken)
    {
        run.Status = status;
        run.Error = error;
        run.CompletedUtc = _clock.UtcNow;
        await _runStore.UpdateAsync(run, cancellationToken);

        if (testCase is not null)
        {
            testCase.LastStatus = status;
            testCase.LastRunUtc = run.CompletedUtc;
            await _testCaseStore.UpdateAsync(testCase, cancellationToken);
        }

        return run;
    }
}
