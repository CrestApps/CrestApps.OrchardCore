using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Controllers;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Handlers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIConversationQualityTests
{
    private static readonly DateTime _now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SplitTraffic_ReadsWeightedOutcomes_AndSkipsBadLines()
    {
        var variants = SplitTrafficTask.ParseVariants("A: 50\nB: 30%\nC: 0\nnot a line\nA: 10\nTest group: 20");

        Assert.Equal([("A", 50d), ("B", 30d), ("Test group", 20d)], variants);
    }

    [Theory]
    [InlineData(0.0, "A")]
    [InlineData(0.79, "A")]
    [InlineData(0.8, "B")]
    [InlineData(0.999, "B")]
    public void SplitTraffic_PicksTheOutcomeWhoseShareCoversThePosition(double position, string expected)
    {
        Assert.Equal(expected, SplitTrafficTask.Pick([("A", 80), ("B", 20)], position));
    }

    [Fact]
    public void SplitTraffic_KeepsAKeyOnTheSameOutcome_AndSpreadsKeysByWeight()
    {
        Assert.Equal(SplitTrafficTask.Position("workflow|+15551234567"), SplitTrafficTask.Position("workflow|+15551234567"));

        var variants = new List<(string Name, double Weight)> { ("A", 50), ("B", 50) };
        var a = Enumerable.Range(0, 2000).Count(i => SplitTrafficTask.Pick(variants, SplitTrafficTask.Position("w|" + i)) == "A");

        Assert.InRange(a, 900, 1100);
    }

    [Fact]
    public void Evaluator_ClampsTheScore_AndFailsWhenACriterionIsNotMet()
    {
        var request = new AIConversationEvaluationRequest
        {
            Criteria = ["Confirmed the date", "Offered a reminder"],
            PassingScore = 70,
        };

        var evaluation = AIConversationEvaluator.ToEvaluation(new AIConversationJudgeVerdict
        {
            Score = 140,
            Sentiment = "NEGATIVE",
            Criteria =
            [
                new AIConversationCriterionResult { Name = "confirmed the date", Met = true, Reason = "It did." },
            ],
            Issues =
            [
                new AIConversationIssue { Kind = "toolfailure", Detail = "The booking tool failed.", Quote = "Assistant: Booked!" },
                new AIConversationIssue { Kind = "Weird", Detail = "Something else." },
                new AIConversationIssue { Kind = "Tone" },
            ],
        }, request, _now);

        Assert.Equal(100, evaluation.Score);
        Assert.Equal("Negative", evaluation.Sentiment);
        Assert.Collection(
            evaluation.Criteria,
            criterion => Assert.True(criterion.Met),
            criterion =>
            {
                Assert.Equal("Offered a reminder", criterion.Name);
                Assert.False(criterion.Met);
            });
        Assert.Equal([AIConversationIssueKinds.ToolFailure, AIConversationIssueKinds.Other], evaluation.Issues.Select(issue => issue.Kind));
        Assert.False(evaluation.Passed);
        Assert.Equal(_now, evaluation.EvaluatedUtc);
    }

    [Fact]
    public void Evaluator_MatchesRewordedCriteriaByNumberOrPosition()
    {
        var request = new AIConversationEvaluationRequest
        {
            Criteria = ["Confirms the order number back.", "Ends by offering more help."],
            PassingScore = 70,
        };

        // Numbered as the request listed them.
        var numbered = AIConversationEvaluator.ToEvaluation(new AIConversationJudgeVerdict
        {
            Score = 90,
            Criteria =
            [
                new AIConversationCriterionResult { Name = "2. Offers more help at the end", Met = true, Reason = "Asked." },
                new AIConversationCriterionResult { Name = "1. Confirms the order number back.", Met = true, Reason = "Read it back." },
            ],
        }, request, _now);

        Assert.Equal(["Read it back.", "Asked."], numbered.Criteria.Select(criterion => criterion.Reason));
        Assert.True(numbered.Passed);

        // Every name reworded, but one answer per criterion in order.
        var reworded = AIConversationEvaluator.ToEvaluation(new AIConversationJudgeVerdict
        {
            Score = 90,
            Criteria =
            [
                new AIConversationCriterionResult { Name = "Order number confirmation", Met = true, Reason = "Yes." },
                new AIConversationCriterionResult { Name = "Closing offer of help", Met = false, Reason = "No." },
            ],
        }, request, _now);

        Assert.Equal([true, false], reworded.Criteria.Select(criterion => criterion.Met));
        Assert.Equal("Ends by offering more help.", reworded.Criteria[1].Name);
    }

    [Fact]
    public void Evaluator_PassesOnTheScore_WhenThereAreNoCriteria()
    {
        var request = new AIConversationEvaluationRequest { PassingScore = 70 };

        Assert.True(AIConversationEvaluator.ToEvaluation(new AIConversationJudgeVerdict { Score = 70 }, request, _now).Passed);
        Assert.False(AIConversationEvaluator.ToEvaluation(new AIConversationJudgeVerdict { Score = 69 }, request, _now).Passed);
        Assert.False(AIConversationEvaluator.ToEvaluation(null, request, _now).Passed);
    }

    [Fact]
    public void Evaluator_GivesTheJudgeTheInstructionsCriteriaAndTranscript()
    {
        var text = AIConversationEvaluator.BuildRequest(new AIConversationEvaluationRequest
        {
            Instructions = "Never promise refunds.",
            Criteria = AIConversationEvaluator.ParseCriteria("- Confirmed the date\n\n* Was polite"),
            Turns =
            [
                new AIConversationTurn(AIConversationTurn.CustomerRole, "Can I move it?", _now),
                new AIConversationTurn(AIConversationTurn.AssistantRole, "Sure, to when?", _now),
            ],
        });

        Assert.Contains("Never promise refunds.", text);
        Assert.Contains("1. Confirmed the date", text);
        Assert.Contains("2. Was polite", text);
        Assert.Contains("Customer: Can I move it?", text);
        Assert.Contains("Assistant: Sure, to when?", text);
    }

    [Fact]
    public void Simulator_ReadsTheCustomersLine_AndTheEndMarker()
    {
        Assert.Equal(("Thanks, that's all.", true), AIConversationSimulator.ReadCustomerLine("Thanks, that's all. [END]"));
        Assert.Equal(("I need to reschedule.", false), AIConversationSimulator.ReadCustomerLine("Customer: I need to reschedule."));
        Assert.Equal((string.Empty, true), AIConversationSimulator.ReadCustomerLine("[end]"));
    }

    [Fact]
    public void Simulator_TurnsTheRolesAround_ForTheSimulatedCustomer()
    {
        var turns = new[]
        {
            new AIConversationTurn(AIConversationTurn.CustomerRole, "Hi", _now),
            new AIConversationTurn(AIConversationTurn.AssistantRole, "Hello, how can I help?", _now),
        };

        var customer = AIConversationSimulator.BuildCustomerMessages("You are Dana.", turns);
        var assistant = AIConversationSimulator.BuildAssistantMessages(turns);

        Assert.Equal(ChatRole.System, customer[0].Role);
        Assert.Contains("You are Dana.", customer[0].Text);
        Assert.Equal([ChatRole.Assistant, ChatRole.User], customer.Skip(1).Select(message => message.Role));
        Assert.Equal([ChatRole.User, ChatRole.Assistant], assistant.Select(message => message.Role));

        var opening = AIConversationSimulator.BuildCustomerMessages(null, []);
        Assert.Equal(ChatRole.User, opening[^1].Role);
    }

    [Fact]
    public void Metrics_MeasureEvaluations()
    {
        var rows = new[]
        {
            new AIConversationEvaluationIndex { Score = 90, Passed = true },
            new AIConversationEvaluationIndex { Score = 40, Passed = false, IssueKinds = "Hallucination,ToolFailure" },
            new AIConversationEvaluationIndex { Score = 50, Passed = false, IssueKinds = "Unresolved" },
            new AIConversationEvaluationIndex { Score = 100, Passed = true },
        };

        Assert.Equal(new AIConversationMetricValue(70, 4), AIConversationMetrics.MeasureEvaluations(AIConversationMetric.AverageQAScore, rows));
        Assert.Equal(new AIConversationMetricValue(50, 4), AIConversationMetrics.MeasureEvaluations(AIConversationMetric.QAFailureRate, rows));
        Assert.Equal(new AIConversationMetricValue(25, 4), AIConversationMetrics.MeasureEvaluations(AIConversationMetric.ToolFailureRate, rows));
        Assert.Equal(new AIConversationMetricValue(null, 0), AIConversationMetrics.MeasureEvaluations(AIConversationMetric.QAFailureRate, []));
    }

    [Fact]
    public void Metrics_MeasureCallsAndSessions()
    {
        var calls = new[]
        {
            new AIVoiceSessionSummaryIndex { Outcome = nameof(AIVoiceSessionOutcome.HandedToAgent), TimeToFirstAssistantAudioMs = 800 },
            new AIVoiceSessionSummaryIndex { Outcome = nameof(AIVoiceSessionOutcome.Transferred) },
            new AIVoiceSessionSummaryIndex { Outcome = nameof(AIVoiceSessionOutcome.Failed), TimeToFirstAssistantAudioMs = 1200 },
            new AIVoiceSessionSummaryIndex { Outcome = nameof(AIVoiceSessionOutcome.CompletedByAI) },
        };

        Assert.Equal(new AIConversationMetricValue(50, 4), AIConversationMetrics.MeasureCalls(AIConversationMetric.AICallHandoffRate, calls));
        Assert.Equal(new AIConversationMetricValue(25, 4), AIConversationMetrics.MeasureCalls(AIConversationMetric.AICallFailureRate, calls));
        Assert.Equal(new AIConversationMetricValue(1000, 2), AIConversationMetrics.MeasureCalls(AIConversationMetric.AverageTimeToFirstAudioMs, calls));

        var sessions = new[]
        {
            new AIChatSessionEvent { IsResolved = true, ThumbsDownCount = 1 },
            new AIChatSessionEvent { IsResolved = false, UserRating = false },
            new AIChatSessionEvent { IsResolved = true },
        };

        Assert.Equal(new AIConversationMetricValue(66.67, 3), AIConversationMetrics.MeasureSessions(AIConversationMetric.ResolutionRate, sessions));
        Assert.Equal(new AIConversationMetricValue(66.67, 3), AIConversationMetrics.MeasureSessions(AIConversationMetric.NegativeFeedbackRate, sessions));
        Assert.Equal(new AIConversationMetricValue(3, 3), AIConversationMetrics.MeasureSessions(AIConversationMetric.Conversations, sessions));
    }

    [Theory]
    [InlineData(AIConversationAlertCondition.Above, 10, 12, null, true)]
    [InlineData(AIConversationAlertCondition.Above, 10, 10, null, false)]
    [InlineData(AIConversationAlertCondition.Below, 80, 70, null, true)]
    [InlineData(AIConversationAlertCondition.RisesByPercent, 50, 16, 10d, true)]
    [InlineData(AIConversationAlertCondition.RisesByPercent, 50, 14, 10d, false)]
    [InlineData(AIConversationAlertCondition.FallsByPercent, 20, 70, 100d, true)]
    [InlineData(AIConversationAlertCondition.RisesByPercent, 50, 16, null, false)]
    public void Alert_ComparesTheValueWithTheThreshold(AIConversationAlertCondition condition, double threshold, double value, double? previous, bool expected)
    {
        var crossed = CheckConversationMetricTask.IsCrossed(
            condition,
            threshold,
            new AIConversationMetricValue(value, 20),
            previous is null ? null : new AIConversationMetricValue(previous, 20),
            10);

        Assert.Equal(expected, crossed);
    }

    [Fact]
    public void Alert_NeverFiresOnTooFewSamples()
    {
        Assert.False(CheckConversationMetricTask.IsCrossed(AIConversationAlertCondition.Above, 5, new AIConversationMetricValue(50, 2), null, 10));
        Assert.False(CheckConversationMetricTask.IsCrossed(AIConversationAlertCondition.Above, 5, new AIConversationMetricValue(null, 0), null, 0));
    }

    [Fact]
    public void RerunAnalysis_ForgetsTheAnalysis_ButKeepsTheMetrics()
    {
        var session = new AIChatSession
        {
            IsPostSessionTasksProcessed = true,
            IsConversionGoalsEvaluated = true,
            IsAnalyticsRecorded = true,
            PostSessionProcessingAttempts = 3,
            PostSessionProcessingLastAttemptUtc = _now,
        };
        session.PostSessionResults["summary"] = new PostSessionResult();

        RerunConversationAnalysisTask.Reset(session);

        Assert.Empty(session.PostSessionResults);
        Assert.False(session.IsPostSessionTasksProcessed);
        Assert.False(session.IsConversionGoalsEvaluated);
        Assert.True(session.IsAnalyticsRecorded);
        Assert.Equal(0, session.PostSessionProcessingAttempts);
        Assert.Null(session.PostSessionProcessingLastAttemptUtc);
    }

    [Fact]
    public async Task ProfileVersions_NumberEachSave_AndKeepTheRestoreNoteOffTheProfile()
    {
        var store = new InMemoryVersionStore();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);
        var handler = new AIProfileVersionHandler(store, new HttpContextAccessor(), clock.Object);
        var profile = new AIProfile { ItemId = "profile1", DisplayText = "Support" };

        await handler.CreatingAsync(new CreatingContext<AIProfile>(profile), TestContext.Current.CancellationToken);
        await handler.CreatedAsync(new CreatedContext<AIProfile>(profile), TestContext.Current.CancellationToken);

        var metadata = profile.GetOrCreate<AIProfileVersionMetadata>();
        metadata.PendingNote = "Restored from version 1";
        profile.Put(metadata);

        await handler.UpdatingAsync(new UpdatingContext<AIProfile>(profile, null), TestContext.Current.CancellationToken);
        await handler.UpdatedAsync(new UpdatedContext<AIProfile>(profile), TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], store.Versions.Select(version => version.Version));
        Assert.Null(store.Versions[0].Note);
        Assert.Equal("Restored from version 1", store.Versions[1].Note);
        Assert.Equal(2, profile.GetOrCreate<AIProfileVersionMetadata>().Version);
        Assert.Null(profile.GetOrCreate<AIProfileVersionMetadata>().PendingNote);
        Assert.Null(store.Versions[1].Snapshot.GetOrCreate<AIProfileVersionMetadata>().PendingNote);
    }

    [Fact]
    public void ProfileVersions_RestoreKeepsWhatIdentifiesTheProfile()
    {
        var profile = new AIProfile { ItemId = "p1", Name = "support", DisplayText = "Support", ChatDeploymentName = "gpt-new", OwnerId = "owner", CreatedUtc = _now };
        var snapshot = new AIProfile { ItemId = "other", Name = "old-name", DisplayText = "Support (old)", ChatDeploymentName = "gpt-old", OwnerId = "someone", WelcomeMessage = "Hi!" };
        snapshot.Put(new AIProfileMetadata { SystemMessage = "Be brief." });

        AIProfileVersionsController.Restore(profile, snapshot);

        Assert.Equal("p1", profile.ItemId);
        Assert.Equal("support", profile.Name);
        Assert.Equal("owner", profile.OwnerId);
        Assert.Equal(_now, profile.CreatedUtc);
        Assert.Equal("Support (old)", profile.DisplayText);
        Assert.Equal("gpt-old", profile.ChatDeploymentName);
        Assert.Equal("Hi!", profile.WelcomeMessage);
        Assert.Equal("Be brief.", profile.GetOrCreate<AIProfileMetadata>().SystemMessage);
    }

    private sealed class InMemoryVersionStore : IAIProfileVersionStore
    {
        public List<AIProfileVersion> Versions { get; } = [];

        public Task SaveAsync(AIProfileVersion version, CancellationToken cancellationToken = default)
        {
            Versions.Add(version);

            return Task.CompletedTask;
        }

        public Task<AIProfileVersion> FindByIdAsync(string itemId, CancellationToken cancellationToken = default)
            => Task.FromResult(Versions.FirstOrDefault(version => version.ItemId == itemId));

        public Task<int> GetLatestVersionAsync(string profileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Versions.Where(version => version.ProfileId == profileId).Select(version => version.Version).DefaultIfEmpty().Max());

        public Task<(IReadOnlyList<AIProfileVersion> Versions, int Count)> PageAsync(string profileId, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<(IReadOnlyList<AIProfileVersion>, int)>((Versions, Versions.Count));
    }
}
