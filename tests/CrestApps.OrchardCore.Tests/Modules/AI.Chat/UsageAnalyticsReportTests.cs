using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The AI usage report: the completion table, which can now be broken down by model, deployment, profile or
/// connection and filtered to one profile, and the voice section built from the per-call summaries.
/// </summary>
public sealed class UsageAnalyticsReportTests
{
    private static readonly Dictionary<string, string> _profileNames = new(StringComparer.Ordinal)
    {
        ["profile-1"] = "Service reminders",
        ["profile-2"] = "Sales follow-up",
    };

    [Fact]
    public void TheCompletionTable_KeepsOnlyCompletionsThatBelongToAConversation_AndOnlyTheChosenProfile()
    {
        // Arrange
        var records = new[]
        {
            Completion("session-1", "profile-1", tokens: 10),
            Completion("session-2", "profile-2", tokens: 20),
            Completion(sessionId: null, "profile-1", tokens: 40),
        };

        // Act
        var all = AICompletionUsageReport.Relevant(records, profileId: null);
        var one = AICompletionUsageReport.Relevant(records, profileId: "profile-2");

        // Assert
        Assert.Equal(2, all.Count);
        Assert.Equal(20, Assert.Single(one).TotalTokenCount);
    }

    [Theory]
    [InlineData(AICompletionUsageGroupBy.Model, "gpt-4.1", "gpt-4.1-mini")]
    [InlineData(AICompletionUsageGroupBy.Deployment, "chat-main", "chat-small")]
    [InlineData(AICompletionUsageGroupBy.Profile, "Service reminders", "Sales follow-up")]
    [InlineData(AICompletionUsageGroupBy.Connection, "azure-east", "azure-west")]
    public void TheCompletionTable_CanBeGroupedByASingleDimension(AICompletionUsageGroupBy groupBy, string first, string second)
    {
        // Arrange
        // Two completions share everything; the third differs in every dimension, so each grouping splits 2 / 1.
        var records = new[]
        {
            Completion("session-1", "profile-1", tokens: 10, model: "gpt-4.1", deployment: "chat-main", connection: "azure-east"),
            Completion("session-2", "profile-1", tokens: 15, model: "gpt-4.1", deployment: "chat-main", connection: "azure-east"),
            Completion("session-3", "profile-2", tokens: 5, model: "gpt-4.1-mini", deployment: "chat-small", connection: "azure-west"),
        };

        // Act
        var rows = AICompletionUsageReport.BuildRows(records, groupBy, _profileNames);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(first, rows[0].GroupLabel);
        Assert.Equal(25, rows[0].TotalTokens);
        Assert.Equal(2, rows[0].TotalCalls);
        Assert.Equal(second, rows[1].GroupLabel);
    }

    [Fact]
    public void TheCompletionTable_StillGroupsByUserAndModel_ByDefault()
    {
        // Arrange
        var records = new[]
        {
            Completion("session-1", "profile-1", tokens: 10, userName: "ada"),
            Completion("session-2", "profile-1", tokens: 5, userName: "grace"),
        };

        // Act
        var rows = AICompletionUsageReport.BuildRows(records, AICompletionUsageGroupBy.UserAndModel, _profileNames);

        // Assert
        Assert.Equal(["ada", "grace"], rows.Select(row => row.UserLabel));
        Assert.All(rows, row => Assert.Null(row.GroupLabel));
    }

    [Fact]
    public void TextTokens_AreTotalledPerChatSession()
    {
        // Arrange
        var records = new[]
        {
            Completion("session-1", "profile-1", tokens: 10),
            Completion("session-1", "profile-1", tokens: 15),
            Completion("session-2", "profile-1", tokens: 5),
        };

        // Act
        var tokens = AICompletionUsageReport.TokensBySession(records);

        // Assert
        Assert.Equal(25, tokens["session-1"]);
        Assert.Equal(5, tokens["session-2"]);
    }

    [Fact]
    public void TheVoiceTotals_AddUpTheCalls_AndAverageOnlyWhatWasMeasured()
    {
        // Arrange
        // A live call measured everything; a turn-based call could not hear the caller or be talked over; a call
        // nobody answered measured nothing at all.
        var calls = new[]
        {
            Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.HandedToAgent, sessionMs: 120_000, assistantMs: 60_000, callerMs: 30_000, silenceMs: 30_000, bargeIns: 2, firstAudioMs: 800, session: "session-1"),
            Call("activity-2", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.CompletedByAI, sessionMs: 60_000, assistantMs: 30_000, idlePrompts: 1, firstAudioMs: 1_200, session: "session-2"),
            Call("activity-3", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.NoAnswer),
        };
        var textTokens = new Dictionary<string, long>(StringComparer.Ordinal) { ["session-1"] = 100, ["session-2"] = 250 };

        // Act
        var totals = AIVoiceUsageReport.Summarize(label: null, calls, textTokens);

        // Assert
        Assert.Equal(3, totals.Calls);
        Assert.Equal(3, totals.SessionMinutes);
        Assert.Equal(1.5, totals.AIMinutes);
        Assert.Equal(0.5, totals.CallerMinutes);
        Assert.Equal(0.5, totals.SilenceMinutes);
        Assert.Equal(90, totals.AverageSessionSeconds);
        Assert.Equal(1_000, totals.AverageTimeToFirstAudioMs);
        Assert.Equal(1d / 3, totals.HandoffRate, precision: 6);
        Assert.Equal(2, totals.BargeInsPerCall);
        Assert.Equal(1, totals.IdlePrompts);
        Assert.Equal(350, totals.TextTokens);
        Assert.Null(totals.AudioTokens);
    }

    [Fact]
    public void AudioTokens_AreShown_OnceTheProviderReportsThem()
    {
        // Arrange
        var call = Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, sessionMs: 1_000);
        call.InputAudioTokens = 300;
        call.OutputAudioTokens = 700;

        // Act
        var totals = AIVoiceUsageReport.Summarize(label: null, [call], new Dictionary<string, long>());

        // Assert
        Assert.Equal(1_000, totals.AudioTokens);
    }

    [Fact]
    public void ACallSummarizedTwice_IsCountedOnce_AsTheSummaryThatMeasuredIt()
    {
        // Arrange
        // The hangup and the end of the live session can both land before either sees the other.
        var rows = new[]
        {
            Call("activity-1", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.CallerHungUp),
            Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CallerHungUp, sessionMs: 30_000),
            Call("activity-2", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, sessionMs: 10_000),

            // A turn-based call measured at its handoff, and summarized again, later and blind, at its hangup.
            Call("activity-3", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.HandedToAgent, sessionMs: 45_000, startedUtc: new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc)),
            Call("activity-3", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.HandedToAgent, startedUtc: new DateTime(2026, 9, 24, 12, 5, 0, DateTimeKind.Utc)),
        };

        // Act
        var calls = AIVoiceUsageReport.Calls(rows, profileId: null);

        // Assert
        Assert.Equal(3, calls.Count);
        Assert.Equal(AIVoiceSessionEngine.Realtime.ToString(), calls.Single(call => call.ActivityId == "activity-1").Engine);
        Assert.Equal(45_000, calls.Single(call => call.ActivityId == "activity-3").SessionDurationMs);
    }

    [Fact]
    public void TheVoiceSection_FollowsTheProfileFilter()
    {
        // Arrange
        var rows = new[]
        {
            Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, profileId: "profile-1"),
            Call("activity-2", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, profileId: "profile-2"),
        };

        // Act
        var calls = AIVoiceUsageReport.Calls(rows, profileId: "profile-2");

        // Assert
        Assert.Equal("activity-2", Assert.Single(calls).ActivityId);
    }

    [Fact]
    public void TheVoiceTable_ByDeployment_NamesTheModelBehindIt()
    {
        // Arrange
        var calls = new[]
        {
            Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, deployment: "voice-live", model: "gpt-realtime"),
            Call("activity-2", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, deployment: "voice-live", model: "gpt-realtime"),
            Call("activity-3", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.CompletedByAI, deployment: "chat-main", model: "gpt-4.1"),
        };

        // Act
        var rows = AIVoiceUsageReport.BuildRows(calls, AIVoiceUsageGroupBy.Deployment, _profileNames, utc => utc, new Dictionary<string, long>());

        // Assert
        Assert.Equal(["voice-live (gpt-realtime)", "chat-main (gpt-4.1)"], rows.Select(row => row.Label));
        Assert.Equal([2, 1], rows.Select(row => row.Calls));
    }

    [Fact]
    public void TheVoiceTable_ByProfile_UsesTheProfilesCurrentName_AndFallsBackToTheNameItHadThen()
    {
        // Arrange
        var renamed = Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, profileId: "profile-1");
        renamed.AIProfileName = "Old name";
        var deleted = Call("activity-2", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, profileId: "profile-gone");
        deleted.AIProfileName = "Retired profile";

        // Act
        var rows = AIVoiceUsageReport.BuildRows([renamed, deleted], AIVoiceUsageGroupBy.Profile, _profileNames, utc => utc, new Dictionary<string, long>());

        // Assert
        Assert.Equal(["Retired profile", "Service reminders"], rows.Select(row => row.Label).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheVoiceTable_ByCampaign_ByChannel_AndByEngine_NameEachGroup()
    {
        // Arrange
        var campaignCall = Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI);
        campaignCall.CampaignId = "campaign-1";
        campaignCall.CampaignName = "Autumn service";
        var inbound = Call("activity-2", AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.CompletedByAI);
        AIVoiceSessionSummaryIndex[] calls = [campaignCall, inbound];

        // Act
        var byCampaign = AIVoiceUsageReport.BuildRows(calls, AIVoiceUsageGroupBy.Campaign, _profileNames, utc => utc, new Dictionary<string, long>());
        var byChannel = AIVoiceUsageReport.BuildRows(calls, AIVoiceUsageGroupBy.Channel, _profileNames, utc => utc, new Dictionary<string, long>());
        var byEngine = AIVoiceUsageReport.BuildRows(calls, AIVoiceUsageGroupBy.Engine, _profileNames, utc => utc, new Dictionary<string, long>());

        // Assert
        Assert.Equal(["Autumn service", "No campaign"], byCampaign.Select(row => row.Label).Order(StringComparer.Ordinal));
        Assert.Equal("Phone", Assert.Single(byChannel).Label);
        Assert.Equal(["Realtime", "Turn-based"], byEngine.Select(row => row.Label).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheVoiceTable_ByDay_UsesTheLocalDayTheCallStarted_InDateOrder()
    {
        // Arrange
        // 23:30 UTC is already the next day six hours east.
        var late = Call("activity-1", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, startedUtc: new DateTime(2026, 9, 23, 23, 30, 0, DateTimeKind.Utc));
        var early = Call("activity-2", AIVoiceSessionEngine.Realtime, AIVoiceSessionOutcome.CompletedByAI, startedUtc: new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc));

        // Act
        var rows = AIVoiceUsageReport.BuildRows([late, early], AIVoiceUsageGroupBy.Day, _profileNames, utc => utc.AddHours(6), new Dictionary<string, long>());

        // Assert
        Assert.Equal(["2026-09-23", "2026-09-24"], rows.Select(row => row.Label));
    }

    private static AICompletionUsageRecord Completion(
        string sessionId,
        string profileId,
        int tokens,
        string model = "gpt-4.1",
        string deployment = "chat-main",
        string connection = "azure-east",
        string userName = "ada")
        => new()
        {
            SessionId = sessionId,
            ProfileId = profileId,
            TotalTokenCount = tokens,
            ModelName = model,
            DeploymentName = deployment,
            ConnectionName = connection,
            UserName = userName,
            IsAuthenticated = true,
            ClientName = "Azure",
        };

    private static AIVoiceSessionSummaryIndex Call(
        string activityId,
        AIVoiceSessionEngine engine,
        AIVoiceSessionOutcome outcome,
        long? sessionMs = null,
        long? assistantMs = null,
        long? callerMs = null,
        long? silenceMs = null,
        int? bargeIns = null,
        int idlePrompts = 0,
        long? firstAudioMs = null,
        string session = null,
        string profileId = "profile-1",
        string deployment = "voice-live",
        string model = "gpt-realtime",
        DateTime? startedUtc = null)
        => new()
        {
            ActivityId = activityId,
            AISessionId = session,
            AIProfileId = profileId,
            Channel = "Phone",
            Engine = engine.ToString(),
            Outcome = outcome.ToString(),
            DeploymentName = deployment,
            ModelName = model,
            StartedUtc = startedUtc,
            CreatedUtc = startedUtc ?? new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            SessionDurationMs = sessionMs,
            AssistantSpeakingMs = assistantMs,
            CallerSpeakingMs = callerMs,
            MutualSilenceMs = silenceMs,
            TimeToFirstAssistantAudioMs = firstAudioMs,
            BargeIns = bargeIns ?? (engine == AIVoiceSessionEngine.Realtime && sessionMs.HasValue ? 0 : null),
            IdlePrompts = idlePrompts,
        };
}
