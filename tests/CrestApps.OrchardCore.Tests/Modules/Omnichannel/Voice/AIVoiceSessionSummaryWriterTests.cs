using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The one summary an automated call leaves for the AI usage report: the activity's who-and-where, the deployment
/// that actually held the call, the measured audio, and the outcome -- written once, and only when the tenant has
/// asked for usage to be tracked.
/// </summary>
public sealed class AIVoiceSessionSummaryWriterTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ALiveCall_IsSummarizedWithWhoHeldItWhereAndWhatWasMeasured()
    {
        // Arrange
        var harness = new WriterHarness();
        harness.Says(
            (ChatRole.Assistant, "Hi, is this Jamie?"),
            (ChatRole.User, "Yes, speaking."),
            (ChatRole.Assistant, "Great. I'm calling about your service appointment."));

        var started = _now.AddMinutes(-2);

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            Engine = AIVoiceSessionEngine.Realtime,
            DeploymentName = "realtime-deployment",
            Outcome = AIVoiceSessionOutcome.HandedToAgent,
            WasAnswered = true,
            Measurements = new AIVoiceSessionMeasurements
            {
                StartedUtc = started,
                EndedUtc = started.AddSeconds(90),
                SessionDurationMs = 90_000,
                AssistantSpeakingMs = 40_000,
                CallerSpeakingMs = 30_000,
                MutualSilenceMs = 20_000,
                TimeToFirstAssistantAudioMs = 800,
                BargeIns = 2,
                IdlePrompts = 1,
                EchoHeldMs = 1_200,
            },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(summary);
        Assert.Same(summary, Assert.Single(harness.Saved));
        Assert.False(string.IsNullOrEmpty(summary.ItemId));
        Assert.Equal("activity-1", summary.ActivityId);
        Assert.Equal("session-1", summary.AISessionId);
        Assert.Equal("profile-1", summary.AIProfileId);
        Assert.Equal("Service reminders", summary.AIProfileName);
        Assert.Equal("campaign-1", summary.CampaignId);
        Assert.Equal("Autumn service", summary.CampaignName);
        Assert.Equal(OmnichannelConstants.Channels.Phone, summary.Channel);
        Assert.Equal("endpoint-1", summary.ChannelEndpointId);
        Assert.Equal("Telnyx", summary.ProviderName);
        Assert.Equal("call-1", summary.ProviderCallId);
        Assert.Equal(AIVoiceSessionEngine.Realtime, summary.Engine);
        Assert.Equal("realtime-deployment", summary.DeploymentName);
        Assert.Equal("gpt-realtime", summary.ModelName);
        Assert.Equal("azure-east", summary.ConnectionName);
        Assert.Equal(AIVoiceSessionOutcome.HandedToAgent, summary.Outcome);
        Assert.Equal(started, summary.StartedUtc);
        Assert.Equal(started.AddSeconds(90), summary.EndedUtc);
        Assert.Equal(_now, summary.CreatedUtc);
        Assert.Equal(90_000, summary.SessionDurationMs);
        Assert.Equal(40_000, summary.AssistantSpeakingMs);
        Assert.Equal(30_000, summary.CallerSpeakingMs);
        Assert.Equal(20_000, summary.MutualSilenceMs);
        Assert.Equal(800, summary.TimeToFirstAssistantAudioMs);
        Assert.Equal(2, summary.BargeIns);
        Assert.Equal(1, summary.IdlePrompts);
        Assert.Equal(1_200, summary.EchoHeldMs);
        Assert.Equal(2, summary.AssistantTurns);
        Assert.Equal(1, summary.CallerTurns);

        // Realtime token usage is not reported by the framework yet, so it is unknown rather than zero.
        Assert.Null(summary.InputAudioTokens);
        Assert.Null(summary.OutputAudioTokens);
    }

    [Fact]
    public async Task ATurnBasedCall_RecordsTheDeploymentItsProfileCompletesOn_AndReadsTheRestFromTheTranscript()
    {
        // Arrange
        // A turn-based call names no deployment of its own: its replies come from the profile's chat deployment.
        // Its idle prompts are the "are you still there?" lines it said, and it is never talked over -- it stops
        // listening while it speaks -- so barge-ins do not apply to it.
        var harness = new WriterHarness();
        harness.Activity.Status = ActivityStatus.Completed;
        harness.Says(
            (ChatRole.Assistant, "Hi, is this Jamie?"),
            (ChatRole.Assistant, VoiceAgentConversationLoop.StillThereLine),
            (ChatRole.User, "Sorry, yes."),
            (ChatRole.Assistant, "Thanks, goodbye. " + VoiceAgentConversationLoop.HangupMarker));

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.TurnBased,
            WasAnswered = true,
            Measurements = new AIVoiceSessionMeasurements
            {
                StartedUtc = _now.AddSeconds(-30),
                EndedUtc = _now,
                SessionDurationMs = 30_000,
                AssistantSpeakingMs = 9_000,
            },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("chat-deployment", summary.DeploymentName);
        Assert.Equal("gpt-4.1", summary.ModelName);
        Assert.Equal(AIVoiceSessionOutcome.CompletedByAI, summary.Outcome);
        Assert.Equal(1, summary.IdlePrompts);
        Assert.Equal(3, summary.AssistantTurns);
        Assert.Equal(1, summary.CallerTurns);
        Assert.Null(summary.BargeIns);
        Assert.Null(summary.CallerSpeakingMs);
    }

    [Fact]
    public async Task ACallThisNodeNeverMeasured_IsStillSummarized_WithItsDurationsUnknown()
    {
        // Arrange
        var harness = new WriterHarness();
        harness.Activity.Status = ActivityStatus.Completed;

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.TurnBased,
            WasAnswered = false,
            EndedUtc = _now,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIVoiceSessionOutcome.NoAnswer, summary.Outcome);
        Assert.Null(summary.StartedUtc);
        Assert.Equal(_now, summary.EndedUtc);
        Assert.Null(summary.SessionDurationMs);
        Assert.Null(summary.AssistantSpeakingMs);
    }

    [Fact]
    public async Task ACallAlreadySummarized_IsNotSummarizedAgain()
    {
        // Arrange
        // A live call is summarized when its session ends, and the provider's hangup for the same call arrives
        // afterwards and asks again, knowing nothing about the audio.
        var harness = new WriterHarness();
        harness.Existing.Add(new AIVoiceSessionSummary { ActivityId = "activity-1", Engine = AIVoiceSessionEngine.Realtime, SessionDurationMs = 60_000 });

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.TurnBased,
            WasAnswered = true,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(summary);
        Assert.Empty(harness.Saved);
        Assert.Empty(harness.Deleted);
    }

    [Fact]
    public async Task AnUnmeasuredSummaryWrittenFirst_GivesWayToTheMeasuredOne()
    {
        // Arrange
        // The hangup and the end of the live session race each other. When the hangup wins, the summary it wrote
        // knows nothing about the audio, and the session's own summary is the one worth keeping.
        var harness = new WriterHarness();
        var unmeasured = new AIVoiceSessionSummary { ActivityId = "activity-1", Engine = AIVoiceSessionEngine.TurnBased };
        harness.Existing.Add(unmeasured);

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.Realtime,
            DeploymentName = "realtime-deployment",
            Outcome = AIVoiceSessionOutcome.CallerHungUp,
            WasAnswered = true,
            Measurements = new AIVoiceSessionMeasurements { StartedUtc = _now.AddMinutes(-1), EndedUtc = _now, SessionDurationMs = 60_000 },
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(summary);
        Assert.Same(unmeasured, Assert.Single(harness.Deleted));
        Assert.Same(summary, Assert.Single(harness.Saved));
    }

    [Fact]
    public async Task ATenantThatHasNotTurnedOnUsageTracking_KeepsNoSummary()
    {
        // Arrange
        var harness = new WriterHarness(trackingEnabled: false);

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.Realtime,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(summary);
        Assert.Empty(harness.Saved);
    }

    [Fact]
    public async Task ADeploymentNoLongerInTheCatalog_IsStillRecordedByName()
    {
        // Arrange
        var harness = new WriterHarness();

        // Act
        var summary = await harness.Writer.WriteAsync(new AIVoiceSessionDraft
        {
            ActivityId = "activity-1",
            Engine = AIVoiceSessionEngine.Realtime,
            DeploymentName = "since-deleted",
            Outcome = AIVoiceSessionOutcome.CallerHungUp,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("since-deleted", summary.DeploymentName);
        Assert.Null(summary.ModelName);
    }

    private sealed class WriterHarness
    {
        private readonly List<AIChatSessionPrompt> _prompts = [];

        public WriterHarness(bool trackingEnabled = true)
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Status = ActivityStatus.InProgress,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "endpoint-1",
                CampaignId = "campaign-1",
                AIProfileId = "profile-1",
                AISessionId = "session-1",
            };

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore
                .Setup(store => store.FindByIdAsync("activity-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Activity);

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore
                .Setup(store => store.GetPromptsAsync("session-1"))
                .ReturnsAsync(() => _prompts);

            var profile = new AIProfile
            {
                ItemId = "profile-1",
                Name = "service-reminders",
                DisplayText = "Service reminders",
                ChatDeploymentName = "chat-deployment",
            };

            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            var deployments = new Dictionary<string, AIDeployment>(StringComparer.Ordinal)
            {
                ["realtime-deployment"] = new() { Name = "realtime-deployment", ModelName = "gpt-realtime", ConnectionName = "azure-east" },
                ["chat-deployment"] = new() { Name = "chat-deployment", ModelName = "gpt-4.1", ConnectionName = "azure-east" },
            };

            var deploymentManager = new Mock<IAIDeploymentManager>();
            deploymentManager
                .Setup(manager => manager.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string name, CancellationToken _) => deployments.GetValueOrDefault(name));
            deploymentManager
                .Setup(manager => manager.ResolveSlotAsync(
                    AIDeploymentSlotNames.Chat,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string deploymentName, string _, IReadOnlyDictionary<string, string> _, CancellationToken _) =>
                    deployments.GetValueOrDefault(deploymentName ?? string.Empty));

            var campaigns = new Mock<ICatalog<OmnichannelCampaign>>();
            campaigns
                .Setup(catalog => catalog.FindByIdAsync("campaign-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelCampaign { ItemId = "campaign-1", DisplayText = "Autumn service" });

            var summaries = new Mock<IAIVoiceSessionSummaryStore>();
            summaries
                .Setup(store => store.FindByActivityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string activityId, CancellationToken _) => Existing.FirstOrDefault(summary => summary.ActivityId == activityId));
            summaries
                .Setup(store => store.SaveAsync(It.IsAny<AIVoiceSessionSummary>(), It.IsAny<CancellationToken>()))
                .Callback<AIVoiceSessionSummary, CancellationToken>((summary, _) => Saved.Add(summary))
                .Returns(Task.CompletedTask);
            summaries
                .Setup(store => store.DeleteAsync(It.IsAny<AIVoiceSessionSummary>(), It.IsAny<CancellationToken>()))
                .Callback<AIVoiceSessionSummary, CancellationToken>((summary, _) => Deleted.Add(summary))
                .Returns(Task.CompletedTask);
            summaries
                .Setup(store => store.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<AIVoiceSessionSummaryIndex>());

            var options = new Mock<IOptionsMonitor<GeneralAIOptions>>();
            options
                .Setup(monitor => monitor.CurrentValue)
                .Returns(new GeneralAIOptions { EnableAIUsageTracking = trackingEnabled });

            Writer = new AIVoiceSessionSummaryWriter(
                activityStore.Object,
                promptStore.Object,
                profileManager.Object,
                deploymentManager.Object,
                campaigns.Object,
                summaries.Object,
                options.Object,
                new StubClock(_now),
                NullLogger<AIVoiceSessionSummaryWriter>.Instance);
        }

        public OmnichannelActivity Activity { get; }

        public AIVoiceSessionSummaryWriter Writer { get; }

        public List<AIVoiceSessionSummary> Saved { get; } = [];

        public List<AIVoiceSessionSummary> Existing { get; } = [];

        public List<AIVoiceSessionSummary> Deleted { get; } = [];

        public void Says(params (ChatRole Role, string Content)[] turns)
        {
            _prompts.Clear();
            _prompts.AddRange(turns.Select(turn => new AIChatSessionPrompt
            {
                SessionId = "session-1",
                Role = turn.Role,
                Content = turn.Content,
            }));
        }
    }
}
