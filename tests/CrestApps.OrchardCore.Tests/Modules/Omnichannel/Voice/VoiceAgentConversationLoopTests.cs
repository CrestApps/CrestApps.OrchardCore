using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The automated voice conversation, exercised end to end through a provider that records rather than dials. Each
/// case is a turn of a real call: the person picks up, the assistant says something, the person answers, the model
/// decides to escalate. Until the loop was lifted out of the provider module, none of this could be covered
/// without a Telnyx account and a live phone call.
/// </summary>
public sealed class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task WhenTheCallIsAnswered_TheAssistantSpeaksFirst()
    {
        // Arrange
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Assert
        Assert.Single(harness.Media.Spoken);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task WhenTheCallIsAnswered_TheActivityStopsLookingUnanswered()
    {
        // Arrange
        // The background pass that fails calls nobody picked up only transitions activities still awaiting an
        // answer. Leaving this one there lets that pass mark a live conversation failed while it is happening.
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.AwaitingCustomerAnswer;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Assert
        Assert.Equal(ActivityStatus.InProgress, harness.Activity.Status);
    }

    [Fact]
    public async Task TheGreeting_IsSpokenOnce_WhenTheAnsweredEventIsRedelivered()
    {
        // Arrange
        // Provider webhooks arrive at least once. Greeting a person twice is the most visible way that shows up.
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered);
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Assert
        Assert.Single(harness.Media.Spoken);
    }

    [Fact]
    public async Task TheGreeting_UsesTheProvidersDefaultVoice_WhenTheActivityNamesNone()
    {
        // Arrange
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Assert
        Assert.Equal("fake-default-voice", harness.Media.Voices[0]);
    }

    [Fact]
    public async Task TheGreeting_UsesTheActivitysVoice_WhenOneIsConfigured()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Activity.TextToSpeechVoiceId = "chosen-voice";

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Assert
        Assert.Equal("chosen-voice", harness.Media.Voices[0]);
    }

    [Fact]
    public async Task WhenTheAssistantFinishesSpeaking_ItListens()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded);

        // Assert
        Assert.Equal(1, harness.Media.TranscriptionStarts);
    }

    [Fact]
    public async Task WhenThePersonSpeaks_TheReplyIsSpokenAndListeningStops()
    {
        // Arrange
        // Listening through the assistant's own text-to-speech feeds it back in as if the person had said it.
        var harness = new LoopHarness();
        harness.Reply = "We have a few that would suit you.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.");

        // Assert
        Assert.Equal(1, harness.Media.TranscriptionStops);
        Assert.Equal("We have a few that would suit you.", harness.Media.Spoken[^1]);
    }

    [Fact]
    public async Task AnInterimTranscript_IsIgnored()
    {
        // Arrange
        // Answering half a sentence talks over the person saying the rest of it.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered);
        var spokenAfterGreeting = harness.Media.Spoken.Count;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for", isFinal: false);

        // Assert
        Assert.Equal(spokenAfterGreeting, harness.Media.Spoken.Count);
    }

    [Fact]
    public async Task TheSameTranscript_DeliveredTwice_IsAnsweredOnce()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Reply = "Understood.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.");
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.");

        // Assert
        Assert.Equal(1, harness.Media.Spoken.Count(text => text == "Understood."));
    }

    [Fact]
    public async Task WhenTheModelEndsTheCall_TheGoodbyeFinishesBeforeTheHangup()
    {
        // Arrange
        // Hanging up the moment the model decides to stop cuts the closing line off mid-word.
        var harness = new LoopHarness();
        harness.Reply = "Thanks for your time. Goodbye. [[HANGUP]]";
        await harness.HandleAsync(VoiceAgentEventKind.Answered);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "No thanks.");

        // Assert
        Assert.Equal(0, harness.Media.Hangups);
        Assert.DoesNotContain("[[HANGUP]]", harness.Media.Spoken[^1], StringComparison.Ordinal);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded);

        // Assert
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task WhenTheModelEscalates_TheHandoffIsRequestedOnce()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?");

        // Act
        // The escalation is decided during the turn but only acted on once the closing line has been spoken.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded);

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WhenTheHandoffHasNowhereToGo_TheCallEndsRatherThanSittingSilent()
    {
        // Arrange
        // Telling somebody they are being connected and then leaving them listening to nothing is worse than
        // ending the call.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.HandoffResult = OmnichannelHandoffResult.Failure("No queue.");
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded);

        // Assert
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task WhenNoProviderCanCarryTheCall_NothingIsSaid()
    {
        // Arrange
        // A tenant whose provider has no automated voice media should get silence from the loop, not a
        // conversation nobody can hear.
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, providerName: "SomeOtherProvider");

        // Assert
        Assert.Empty(harness.Media.Spoken);
    }

    /// <summary>
    /// Wires the loop up to fakes: an in-memory activity, chat session and transcript, a model that returns
    /// whatever <see cref="Reply"/> is set to, and a provider that records rather than dials.
    /// </summary>
    private sealed class LoopHarness
    {
        private readonly List<AIChatSessionPrompt> _prompts = [];

        public LoopHarness()
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Status = ActivityStatus.AwaitingCustomerAnswer,
                AIProfileId = "profile-1",
                SubjectContentType = "Opportunity",
                Channel = OmnichannelConstants.Channels.Phone,
            };

            var profile = new AIProfile
            {
                ItemId = "profile-1",
                Type = AIProfileType.Chat,
            };

            var session = new AIChatSession
            {
                SessionId = "session-1",
                ProfileId = "profile-1",
            };

            var activityStore = new Mock<IOmnichannelActivityStore>();
            activityStore.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Activity);
            activityStore.Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()));

            var sessionManager = new Mock<IAIChatSessionManager>();
            sessionManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            sessionManager.Setup(x => x.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()));

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore.Setup(x => x.GetPromptsAsync(It.IsAny<string>()))
                .ReturnsAsync(() => _prompts);
            promptStore.Setup(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()))
                .Callback<AIChatSessionPrompt, CancellationToken>((prompt, _) => _prompts.Add(prompt));

            var profileManager = new Mock<IAIProfileManager>();
            profileManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            var completionService = new Mock<IAICompletionService>();
            completionService.Setup(x => x.CompleteAsync(
                    It.IsAny<AIDeployment>(),
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<AICompletionContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)));

            var deploymentManager = new Mock<IAIDeploymentManager>();
            deploymentManager.Setup(x => x.ResolveOrDefaultAsync(
                    It.IsAny<AIDeploymentPurpose>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIDeployment { ItemId = "deployment-1" });

            var contextBuilder = new Mock<IAICompletionContextBuilder>();
            contextBuilder.Setup(x => x.BuildAsync(
                    It.IsAny<AIProfile>(),
                    It.IsAny<Action<AICompletionContext>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AICompletionContext());

            FlowSettingsService = new Mock<ISubjectFlowSettingsService>();
            FlowSettingsService.Setup(x => x.FindConfiguredFlowSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => FlowSettings);

            HandoffService = new Mock<IOmnichannelHandoffService>();
            HandoffService.Setup(x => x.CanHandle(It.IsAny<string>())).Returns(true);
            HandoffService.Setup(x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => HandoffResult);

            HandoffTurn = new Mock<IOmnichannelHandoffTurn>();

            Loop = new VoiceAgentConversationLoop(
                activityStore.Object,
                sessionManager.Object,
                promptStore.Object,
                completionService.Object,
                HandoffTurn.Object,
                deploymentManager.Object,
                contextBuilder.Object,
                profileManager.Object,
                FlowSettingsService.Object,
                [HandoffService.Object],
                // Two providers, so the loop has to resolve by name rather than fall back to the only one there is.
                new VoiceAgentMediaProviderResolver([Media, new FakeVoiceAgentMediaProvider("OtherFake")]),
                Mock.Of<ILiquidTemplateManager>(),
                Mock.Of<IContentManager>(),
                Mock.Of<IClock>(),
                NullLogger<VoiceAgentConversationLoop>.Instance);
        }

        public OmnichannelActivity Activity { get; }

        public FakeVoiceAgentMediaProvider Media { get; } = new();

        public VoiceAgentConversationLoop Loop { get; }

        public Mock<IOmnichannelHandoffService> HandoffService { get; }

        public Mock<IOmnichannelHandoffTurn> HandoffTurn { get; }

        public Mock<ISubjectFlowSettingsService> FlowSettingsService { get; }

        public SubjectFlowSettings FlowSettings { get; private set; }

        public OmnichannelHandoffResult HandoffResult { get; set; } = OmnichannelHandoffResult.Success();

        public string Reply { get; set; } = "Sure, I can help with that.";

        public void EnableHandoff()
        {
            FlowSettings = new SubjectFlowSettings
            {
                EnableAgentHandoff = true,
                HandoffQueueId = "queue-1",
            };
        }

        public Task HandleAsync(
            VoiceAgentEventKind kind,
            string transcript = null,
            bool isFinal = true,
            string providerName = "Fake")
            => Loop.HandleAsync(new VoiceAgentEvent
            {
                Kind = kind,
                ProviderCallId = "call-1",
                ProviderName = providerName,
                ActivityId = Activity.ItemId,
                TranscriptionText = transcript,
                TranscriptionIsFinal = isFinal,
            });
    }
}
