using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
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
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task WhenTheCallIsAnswered_TheAssistantSpeaksFirst()
    {
        // Arrange
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Media.Spoken);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task AProfileWithARealtimeDeployment_HoldsTheCallAsALiveSession()
    {
        // Arrange
        // The turn-based loop cannot start a reply until the caller has stopped, the transcript has come back, the
        // model has answered and the answer has been synthesized. On a phone call that is seconds of dead air per
        // turn, which is what makes an automated call sound automated.
        var harness = new LoopHarness();
        harness.UseRealtime();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Realtime.Sessions);
        Assert.Empty(harness.Media.Spoken);
    }

    [Fact]
    public async Task ARealtimeCall_CarriesTheCampaignsVoiceAndItsOwnTranscript()
    {
        // Arrange
        // Everything downstream — the summary, the disposition, the subject write-back — reads the chat session,
        // so a realtime call has to be handed the same session the turn-based one would have used.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Activity.TextToSpeechVoiceId = "chosen-voice";

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var session = harness.Realtime.Sessions.Single();

        // The same session the activity is now bound to, so the transcript the realtime call writes is the one the
        // conclusion later reads.
        Assert.Equal(harness.Activity.AISessionId, session.Session.SessionId);
        Assert.Equal("chosen-voice", session.Activity.TextToSpeechVoiceId);
        Assert.Equal("Fake", session.ProviderName);
    }

    [Fact]
    public async Task WhenRealtimeCannotRun_TheCallFallsBackToSpeakingAndListening()
    {
        // Arrange
        // A provider with no live-media path, or a session that fails to start, must not leave the person on a
        // silent call: the turn-based loop still works and is better than nothing.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.CanRun = false;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Media.Spoken);
    }

    [Fact]
    public async Task AProfileWithNoRealtimeDeployment_IsNotPutThroughARealtimeSession()
    {
        // Arrange
        // Realtime is chosen on the profile. A profile that has not chosen it keeps exactly the behaviour it had.
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Realtime.Sessions);
        Assert.Single(harness.Media.Spoken);
    }

    [Fact]
    public async Task ARealtimeCall_LeavesTheAwaitingAnswerStateBeforeItStarts()
    {
        // Arrange
        // A realtime session holds the call for its whole duration. Leaving the activity awaiting an answer for
        // that long lets the no-response expiry pass fail a call that is happening right now.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.OnRun = () => Assert.Equal(ActivityStatus.InProgress, harness.Activity.Status);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Realtime.Sessions);
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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Media.Spoken);
    }

    [Fact]
    public async Task TheGreeting_UsesTheProvidersDefaultVoice_WhenTheActivityNamesNone()
    {
        // Arrange
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("chosen-voice", harness.Media.Voices[0]);
    }

    [Fact]
    public async Task WhenTheAssistantFinishesSpeaking_ItListens()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.", cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        var spokenAfterGreeting = harness.Media.Spoken.Count;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for", isFinal: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spokenAfterGreeting, harness.Media.Spoken.Count);
    }

    [Fact]
    public async Task TheSameTranscript_DeliveredTwice_IsAnsweredOnce()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Reply = "Understood.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.", cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, harness.Media.Spoken.Count(text => text == "Understood."));
    }

    [Fact]
    public async Task TheEndCallTool_IsOfferedOnATurnBasedCall()
    {
        // Arrange
        // A call the model cannot end is a call that ends when the customer gives up on it. The tool reached
        // realtime sessions only, so on every turn-based call the assistant said goodbye and then sat there.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "No thanks, I am all set.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(EndCallTool.ToolName, harness.OfferedTools);
    }

    [Fact]
    public async Task ATurnBasedCall_IsToldThatHangingUpIsItsJob()
    {
        // Arrange
        // Offering the tool is not the same as telling the model it is expected to use it. Live, with the tool
        // attached and nothing said about it, the assistant closed the conversation and then waited.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "No thanks, I am all set.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(
            harness.Transcript,
            message => message.Role == ChatRole.System && (message.Text ?? string.Empty).Contains(EndCallTool.ToolName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenTheModelCallsTheEndCallTool_TheCallIsHungUpAfterTheClosingLine()
    {
        // Arrange
        // The tool records on the scoped turn from inside the completion, exactly as it does live; the closing
        // line still has to be spoken in full before the line is cut.
        var harness = new LoopHarness(useRealTurns: true);
        harness.Reply = "No problem at all. Take care.";
        harness.DuringCompletion = () => harness.RealEndCallTurn.RequestEndCall("customer declined");
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "No thanks, I am all set.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("No problem at all. Take care.", harness.Media.Spoken[^1]);
        Assert.Equal(0, harness.Media.Hangups);

        // The closing line finishes.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task WhenTheModelEndsTheCall_TheGoodbyeFinishesBeforeTheHangup()
    {
        // Arrange
        // Hanging up the moment the model decides to stop cuts the closing line off mid-word.
        var harness = new LoopHarness();
        harness.Reply = "Thanks for your time. Goodbye. [[HANGUP]]";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "No thanks.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, harness.Media.Hangups);
        Assert.DoesNotContain("[[HANGUP]]", harness.Media.Spoken[^1], StringComparison.Ordinal);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, harness.Media.Hangups);
    }

    // ---- A line nobody speaks on ----
    //
    // A turn-based call only moves when the provider reports something happened, and silence reports nothing. Live,
    // a voicemail that had finished its greeting was held open for over a minute and a half, recording nothing,
    // until the carrier cut it off: the platform never hung up because it was never told to do anything.

    [Fact]
    public async Task WhenTheAssistantStartsListening_TheLineIsWatchedForSilence()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var armed = Assert.Single(harness.SilenceWatchdog.Armed);
        Assert.Equal(harness.Activity.ItemId, armed.ActivityId);
        Assert.Equal("call-1", armed.ProviderCallId);
        Assert.Equal("Fake", armed.ProviderName);
        Assert.Equal(1, armed.PromptCount);
    }

    [Fact]
    public async Task ASilentLine_IsAskedWhetherTheCallerIsStillThere()
    {
        // Arrange
        // Somebody answered and spoke, and then went quiet: they are asked whether they are still there.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Hi, who is this?", cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.Loop.OnListeningTimedOutAsync(harness.SilenceWatchdog.Armed[^1], TestContext.Current.CancellationToken);

        // Assert
        // Listening stops before speaking, as it does before any reply, so the assistant does not hear itself.
        Assert.Equal(2, harness.Media.TranscriptionStops);
        Assert.Equal(VoiceAgentConversationLoop.StillThereLine, harness.Media.Spoken[^1]);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task ALineThatStaysSilent_IsEnded_AfterTheAssistantHasAskedTwice()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Hi, who is this?", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        // Each prompt is spoken, finishes, and is met with silence again, exactly as on the live call.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
            await harness.Loop.OnListeningTimedOutAsync(harness.SilenceWatchdog.Armed[^1], TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(2, harness.Media.Spoken.Count(text => text == VoiceAgentConversationLoop.StillThereLine));
        Assert.Equal(VoiceAgentConversationLoop.SilentLineGoodbye, harness.Media.Spoken[^1]);

        // The goodbye is heard out before the line is cut, the same way any closing line is.
        Assert.Equal(0, harness.Media.Hangups);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Media.Hangups);
    }

    // ---- A line answered by voicemail ----
    //
    // A recording is heard exactly like somebody answering. Live, "when you have finished recording you may hang up"
    // was replied to as the customer, and the call was concluded as the customer asking not to be called again.

    [Fact]
    public async Task AVoicemailGreeting_IsLeftAMessage_RatherThanRepliedTo()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Reply = "Hi Amani, this is Alex from Prestige Auto Group. Sorry we missed you, we will try again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "When you have finished recording you may hang up.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceAgentConversationLoop.LeavingAVoicemail, harness.Transcript[^1].Text);
        Assert.Equal(ChatRole.User, harness.Transcript[^1].Role);
        Assert.Equal(harness.Reply, harness.Media.Spoken[^1]);
        Assert.True(harness.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.MessageLeft);

        // The message is heard out, and then the call is over: nobody is going to reply to it.
        Assert.Equal(0, harness.Media.Hangups);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task AGreetingStillGoing_IsLetFinish_AndTheMessageIsLeftWhenTheLineGoesQuiet()
    {
        // Arrange
        // Speaking over the rest of the greeting records half a message, or none of it.
        var harness = new LoopHarness();
        harness.Reply = "Hi, this is Alex from Prestige Auto Group. We will try you again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        var spokenBefore = harness.Media.Spoken.Count;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Hi, you've reached Amani.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spokenBefore, harness.Media.Spoken.Count);
        Assert.Empty(harness.Transcript);
        var watch = harness.SilenceWatchdog.Armed[^1];
        Assert.Equal(VoiceAgentConversationLoop.VoicemailToneWait, watch.Wait);

        // The greeting ends and the tone sounds: the quiet that follows is the recording, not a caller gone silent.
        await harness.Loop.OnListeningTimedOutAsync(watch, TestContext.Current.CancellationToken);

        Assert.Equal(harness.Reply, harness.Media.Spoken[^1]);
        Assert.DoesNotContain(VoiceAgentConversationLoop.StillThereLine, harness.Media.Spoken);
    }

    [Fact]
    public async Task AVoicemailWithNothingFromTheModel_IsStillLeftAMessage()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Reply = null;
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Please leave a message after the tone.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceAgentConversationLoop.FallbackVoicemailMessage, harness.Media.Spoken[^1]);
    }

    [Fact]
    public async Task AVoicemailMessageThatAsksAQuestion_IsReplacedWithThePlainMessage()
    {
        // Arrange
        // Live, the model carried on selling to the recording: "are you looking for a new or used vehicle?"
        var harness = new LoopHarness();
        harness.Reply = "Hi Amani, just following up. Are you looking for a new or used vehicle?";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Please record your message. When you have finished recording you may hang up.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceAgentConversationLoop.FallbackVoicemailMessage, harness.Media.Spoken[^1]);
    }

    [Fact]
    public async Task ALineNobodyHasSpokenOn_IsLeftAVoicemailMessage_RatherThanAskedIfAnybodyIsThere()
    {
        // Arrange
        // Live, a short greeting played underneath the opening line and was never heard. The silence after it was
        // asked "are you still there?" twice and told "now isn't a good time", all of it recorded as the message.
        var harness = new LoopHarness();
        harness.Reply = "Hi Amani, this is Alex from Prestige Auto Group. Sorry we missed you, we will try again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.Loop.OnListeningTimedOutAsync(harness.SilenceWatchdog.Armed[^1], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(harness.Reply, harness.Media.Spoken[^1]);
        Assert.DoesNotContain(VoiceAgentConversationLoop.StillThereLine, harness.Media.Spoken);
        Assert.True(harness.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.MessageLeft);

        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Media.Hangups);
    }

    // ---- The provider says who answered ----

    [Fact]
    public async Task AMachineTheProviderDetected_IsMarkedAsVoicemail()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(harness.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.DetectedByProvider);
        Assert.False(voicemail.MessageLeft);
    }

    [Fact]
    public async Task TheAnswerAndTheAnswerer_AreReportedToTheCallObservers()
    {
        // Arrange
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [AutomatedVoiceCallObservationKind.Answered, AutomatedVoiceCallObservationKind.AnswererDetected],
            harness.CallObserver.Observations.Select(observation => observation.Kind));
        Assert.All(harness.CallObserver.Observations, observation => Assert.Equal("call-1", observation.ProviderCallId));
        Assert.All(harness.CallObserver.Observations, observation => Assert.Equal(harness.Activity.ItemId, observation.ActivityItemId));
        Assert.Equal(nameof(VoiceAgentAnswerer.Machine), harness.CallObserver.Observations[1].Answerer);
    }

    [Fact]
    public async Task ACallHandedToAnAgent_ReportsItsConversationEndedThatWay()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Activity.AiEscalated = true;
        harness.Activity.Status = ActivityStatus.InProgress;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var ended = Assert.Single(harness.CallObserver.Observations);
        Assert.Equal(AutomatedVoiceCallObservationKind.ConversationEnded, ended.Kind);
        Assert.Equal("HandedToAgent", ended.Outcome);
    }

    [Fact]
    public async Task ARealtimeCallHandedToAnAgent_ReportsItsConversationEnded_WhenTheSessionEnded_NotWhenTheCallDid()
    {
        // Arrange
        // Live: the realtime session ended at the handoff, and the caller talked to the agent for three and a half
        // minutes more. The conversation's end was reported at the hangup, so the audit said the AI held the call
        // for all of it.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        harness.Realtime.OnRun = () => harness.Clock.Advance(TimeSpan.FromSeconds(27));
        var sessionEndedUtc = harness.Clock.UtcNow.AddSeconds(27);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // The handoff is carried out a moment later, on a scope of its own.
        harness.Clock.Advance(TimeSpan.FromMilliseconds(700));
        await harness.FinishAsync();

        // The caller and the agent talk, and then the caller hangs up.
        harness.Activity.AiEscalated = true;
        harness.Activity.Status = ActivityStatus.InProgress;
        harness.Clock.Advance(TimeSpan.FromMinutes(3.5));
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var ended = harness.CallObserver.Observations
            .Where(observation => observation.Kind == AutomatedVoiceCallObservationKind.ConversationEnded)
            .ToArray();
        Assert.NotEmpty(ended);

        // The first report is the one that counts: an observer keeps the first report of each moment.
        Assert.Equal("HandedToAgent", ended[0].Outcome);
        Assert.Equal(sessionEndedUtc, ended[0].OccurredUtc);
        Assert.Equal("call-1", ended[0].ProviderCallId);
    }

    [Fact]
    public async Task ATurnBasedCallHandedToAnAgent_ReportsItsConversationEnded_AtTheHandoff()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        var handoffUtc = harness.Clock.UtcNow;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var ended = Assert.Single(harness.CallObserver.Observations, observation => observation.Kind == AutomatedVoiceCallObservationKind.ConversationEnded);
        Assert.Equal("HandedToAgent", ended.Outcome);
        Assert.Equal(handoffUtc, ended.OccurredUtc);
    }

    [Fact]
    public async Task APersonTheProviderDetected_IsConversedWith()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Person, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Activity.TryGet<VoicemailReached>(out _));
        Assert.Null(harness.SilenceWatchdog.Armed[^1].Wait);
    }

    [Fact]
    public async Task OnceAMachineIsDetected_TheLineIsListenedToForItsTone()
    {
        // Arrange
        // A person is given twelve seconds to answer. A recording that is waiting for its tone is not, or the
        // message it records is mostly silence.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceAgentConversationLoop.VoicemailToneWait, harness.SilenceWatchdog.Armed[^1].Wait);
    }

    [Fact]
    public async Task AGreetingThatEndsWhileTheAssistantIsSpeaking_IsLeftTheMessageTheMomentItStops()
    {
        // Arrange
        // Spoken the moment the provider said so, the message would queue behind the opening line and the hangup
        // that follows the line would cut it off. It is left when the line finishes instead.
        var harness = new LoopHarness();
        harness.Reply = "Hi, this is Alex from Prestige Auto Group. We will try you again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.MachineGreetingEnded, cancellationToken: TestContext.Current.CancellationToken);
        var spokenBefore = harness.Media.Spoken.Count;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spokenBefore + 1, harness.Media.Spoken.Count);
        Assert.Equal(harness.Reply, harness.Media.Spoken[^1]);
        Assert.Equal(VoiceAgentConversationLoop.LeavingAVoicemail, harness.Transcript[^1].Text);
        Assert.Equal(0, harness.Media.TranscriptionStarts);

        // The message plays, and then the call is over.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task AVoicemailTriggeredAgainWhileItsMessageIsBeingComposed_IsLeftOneMessage()
    {
        // Arrange
        // Composing the message takes seconds, and live, a late transcript of the rest of the greeting arrived in
        // that window and decided the message was still owed: one voicemail was left two.
        var harness = new LoopHarness();
        harness.Reply = "Hi, this is Alex from Prestige Auto Group. We will try you again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        var interrupted = false;
        harness.DuringCompletion = () =>
        {
            if (interrupted)
            {
                return;
            }

            interrupted = true;
            harness.HandleAsync(VoiceAgentEventKind.Transcription, "When you have finished recording you may hang up.", cancellationToken: TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
        };

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Please leave a message after the tone.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(interrupted);
        Assert.Equal(1, harness.Media.Spoken.Count(text => text == harness.Reply));
    }

    [Fact]
    public async Task AGreetingEnd_WithNoMachineDetected_ChangesNothing()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.MachineGreetingEnded, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Activity.TryGet<VoicemailReached>(out _));
        Assert.Equal(1, harness.Media.TranscriptionStarts);
    }

    [Fact]
    public async Task ACustomerWhoMentionsVoicemail_IsRepliedTo()
    {
        // Arrange
        // Once somebody has spoken, the line is a person, whatever they go on to talk about.
        var harness = new LoopHarness();
        harness.Reply = "Got it.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Yes, this is Amani.", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I got your voicemail yesterday, leave a message next time too.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Got it.", harness.Media.Spoken[^1]);
        Assert.False(harness.Activity.TryGet<VoicemailReached>(out _));
        Assert.DoesNotContain(harness.Transcript, message => message.Text == VoiceAgentConversationLoop.LeavingAVoicemail);
    }

    [Fact]
    public async Task ASilenceTheCallerBroke_IsLeftAlone()
    {
        // Arrange
        // The watch fires on a timer, so it can arrive after the caller has already answered. Prompting them
        // then would talk over a conversation that is going perfectly well.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        var watch = harness.SilenceWatchdog.Armed[^1];
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I am looking for a small SUV.", cancellationToken: TestContext.Current.CancellationToken);
        var spokenBefore = harness.Media.Spoken.Count;

        // Act
        await harness.Loop.OnListeningTimedOutAsync(watch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spokenBefore, harness.Media.Spoken.Count);
        Assert.DoesNotContain(VoiceAgentConversationLoop.StillThereLine, harness.Media.Spoken);
    }

    [Fact]
    public async Task ACallThatHasAlreadyEnded_IsNotPromptedOnASilence()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        harness.Activity.Status = ActivityStatus.Completed;
        var spokenBefore = harness.Media.Spoken.Count;

        // Act
        await harness.Loop.OnListeningTimedOutAsync(harness.SilenceWatchdog.Armed[^1], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(spokenBefore, harness.Media.Spoken.Count);
        Assert.Equal(0, harness.Media.TranscriptionStops);
    }

    // ---- The journey a caller asking for a person actually takes ----
    //
    // Three separate defects broke this in production, and each one passed the tests of the day because every
    // test stopped at a seam: the model had no tool to escalate with, the work died with the webhook request it
    // ran inside, and the session never returned so nothing after it ran at all. The caller heard the assistant
    // promise a person and then silence, three times. These follow the whole path instead.

    [Fact]
    public async Task ACallerWhoWasHandedToAQueueAndThenHungUp_IsReleasedFromIt()
    {
        // Arrange
        // The handed-over leg's events are deliberately kept out of Contact Center routing — right up to the
        // handover, wrong after it. Left unsaid, the queue item stayed reserved for somebody no longer on the
        // line, hold music played to a dead leg for a further minute, and the call never counted as abandoned,
        // so the figure that exists to show people giving up read zero while it happened.
        var harness = new LoopHarness();
        harness.Activity.AiEscalated = true;
        harness.Activity.Status = ActivityStatus.InProgress;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([harness.Activity.ItemId], harness.AbandonmentHandler.Released);
    }

    [Fact]
    public async Task ACallThatWasNeverHandedOver_ReleasesNoQueueWork()
    {
        // Arrange
        // An ordinary automated call has no queue behind it. Telling the Contact Center a caller abandoned one
        // would settle work that never existed.
        var harness = new LoopHarness();
        harness.Activity.AiEscalated = false;
        harness.Activity.Status = ActivityStatus.InProgress;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.AbandonmentHandler.Released);
    }

    [Fact]
    public async Task ACallerWhoAsksForAPerson_ReachesTheQueue_EvenWhenTheProviderHasAbandonedTheWebhook()
    {
        // Arrange
        // The real turn objects, because the tool records on one and the loop reads it: a mock of either cannot
        // show that they are the same instance. The request token is already cancelled, which is how an
        // abandoned provider webhook actually arrives -- a voice session holds one open for the length of a call,
        // and no provider waits that long.
        var harness = new LoopHarness(useRealTurns: true);
        harness.EnableHandoff();
        harness.UseRealtime();

        // The session stands in for a live one: while it is held, the model invokes the transfer tool, which is
        // all the tool does -- record the ask on the turn for somebody else to carry out.
        harness.Realtime.OnRun = () => harness.RealHandoffTurn.RequestHandoff("the customer asked for a person");

        // And the completion is carried out rather than merely recorded, as the child scope does on a live call.
        harness.CompletionRunner.FinishOn = harness.Loop;

        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: abandoned.Token);

        // Assert
        // The caller is handed to the queue, which is the whole point of asking for a person.
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // And not on the token that was already dead, which is what silently dropped it in production.
        Assert.False(harness.HandoffToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ACallerWhoAsksForAPerson_IsNotHungUpOnInstead()
    {
        // Arrange
        // The two endings are decided by the same machinery, and the call belongs to the agent now. Hanging up
        // here would drop the person who was just promised one.
        var harness = new LoopHarness(useRealTurns: true);
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.Realtime.OnRun = () => harness.RealHandoffTurn.RequestHandoff("the customer asked for a person");
        harness.CompletionRunner.FinishOn = harness.Loop;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task ACallNobodyAskedToEscalate_ReachesNoQueue()
    {
        // Arrange
        // The other half of the guard, with the real turns: simply holding a live call must never route a caller
        // to an agent. A turn that reported a handoff nobody asked for would send every automated call to a queue.
        var harness = new LoopHarness(useRealTurns: true);
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.CompletionRunner.FinishOn = harness.Loop;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(harness.CompletionRunner.Completion);
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnEscalatedRealtimeCall_IsFinishedAfterTheRequestCarryingItHasBeenAbandoned()
    {
        // Arrange
        // The session runs inside the provider's "call answered" webhook request, for the whole length of the
        // call. No provider waits that long: ours timed out, retried, and the connection was aborted the moment
        // the session ended, taking the handoff with it. Live, the tool recorded the transfer, the assistant went
        // quiet, and the caller was never enqueued — with nothing logged, because the request that would have
        // logged it was already gone.
        //
        // An abandoned request is reproduced here the way it actually arrives: a cancelled token.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: aborted.Token);

        // Assert
        // The call is handed on to be finished elsewhere rather than finished on the dying request.
        Assert.NotNull(harness.CompletionRunner.Completion);
        Assert.True(harness.CompletionRunner.Completion.HandoffRequested);
        Assert.Equal(harness.Activity.ItemId, harness.CompletionRunner.Completion.ActivityId);
        Assert.Equal("call-1", harness.CompletionRunner.Completion.ProviderCallId);
    }

    [Fact]
    public async Task AnEscalatedRealtimeCall_IsStillFinished_WhenTheSessionEndsByFailing()
    {
        // Arrange
        // A session can fail on its way out — a media socket closing badly, a provider connection torn down with
        // the request it was opened on — and it does so after the model has told the caller a person is coming
        // and the tool has recorded it. Hanging the finishing work off the success path meant that failure left
        // the caller on an open, silent line with nothing queued and nobody coming.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        harness.Realtime.OnRun = () => throw new IOException("The media socket closed while the session was shutting down.");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(harness.CompletionRunner.Completion);
        Assert.True(harness.CompletionRunner.Completion.HandoffRequested);
        Assert.Equal(harness.Activity.ItemId, harness.CompletionRunner.Completion.ActivityId);
    }

    [Fact]
    public async Task FinishingAnEscalatedCall_PutsTheCallerInTheQueue()
    {
        // Arrange
        // The other half of the seam: what the child scope does when it gets there. Together these two cover the
        // journey the old test skipped — it asserted the loop enqueues when the flag is set, which stayed green
        // while the live path could neither set the flag nor survive long enough to act on it.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: aborted.Token);

        // Act
        await harness.FinishAsync();

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // And not with the token that was already cancelled: passing the request's token is what dropped the
        // handoff in the first place, silently, because a cancelled token fails before anything is logged.
        Assert.False(harness.HandoffToken.IsCancellationRequested);
    }

    [Fact]
    public async Task WhenARealtimeSessionEndsTheCall_ThePlatformHangsUp()
    {
        // Arrange
        // The session stopping is not the call stopping: the line is still up, and on a realtime call nothing was
        // ever hanging it up. A customer heard the goodbye and then sat on an open line until they gave up.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.EndCallTurn.Setup(x => x.EndCallRequested).Returns(true);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.FinishAsync();

        // Assert
        Assert.Single(harness.Realtime.Sessions);
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task ARealtimeCallTheCallerEnded_IsNotFinishedAgain()
    {
        // Arrange
        // When the caller hangs up first, nothing was decided by the model: there is no handoff to perform and no
        // call left to hang up, so nothing should be scheduled at all.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.EndCallTurn.Setup(x => x.EndCallRequested).Returns(false);
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(false);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Realtime.Sessions);
        Assert.Null(harness.CompletionRunner.Completion);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task AnEscalatedRealtimeCall_IsHandedOverRatherThanHungUp()
    {
        // Arrange
        // Both can be recorded on one call: the model transfers, and the closing machinery also sees the session
        // end. The caller belongs to the agent now, so hanging up would drop the person who was just promised one.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        harness.EndCallTurn.Setup(x => x.EndCallRequested).Returns(true);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.FinishAsync();

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task ARealtimeSessionThatNeverEscalates_HandsOffNobody()
    {
        // Arrange
        // The other half of the guard: holding a realtime call must not by itself route the caller to a queue.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(false);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Realtime.Sessions);
        Assert.Null(harness.CompletionRunner.Completion);
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenTheModelEscalates_TheHandoffIsRequestedOnce()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        // The escalation is decided during the turn but only acted on once the closing line has been spoken.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TheTransferIsAnnouncedOnce_NotOnceForEveryLineItSpeaks()
    {
        // Arrange
        // Announcing the transfer raises another speak.ended, which comes straight back into the same handler.
        // The durable "a transfer is pending" flag used to be cleared only on the after-hours branch, so the
        // ordinary routed transfer never cleared it and every announcement triggered another one. A live caller
        // heard "Thanks for waiting..." seven times in forty-five seconds before hanging up.
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        // The first speak.ended performs the transfer and announces it. The announcement itself then ends, which
        // is the event that used to start the whole thing again.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

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
        await harness.HandleAsync(VoiceAgentEventKind.Answered, providerName: "SomeOtherProvider", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Media.Spoken);
    }

    /// <summary>
    /// A realtime runner that records the session it was asked for instead of opening one.
    /// </summary>
    private sealed class RecordingSilenceWatchdog : ITurnBasedSilenceWatchdog
    {
        public List<TurnBasedSilence> Armed { get; } = [];

        public Task ArmAsync(TurnBasedSilence silence)
        {
            Armed.Add(silence);

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRealtimeRunner : IRealtimeVoiceConversationRunner
    {
        public List<RealtimeVoiceConversationContext> Sessions { get; } = [];

        public bool CanRun { get; set; } = true;

        public Action OnRun { get; set; }

        public Task<bool> RunAsync(RealtimeVoiceConversationContext context, CancellationToken cancellationToken = default)
        {
            OnRun?.Invoke();

            if (!CanRun)
            {
                return Task.FromResult(false);
            }

            Sessions.Add(context);

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Wires the loop up to fakes: an in-memory activity, chat session and transcript, a model that returns
    /// whatever <see cref="Reply"/> is set to, and a provider that records rather than dials.
    /// </summary>
    private sealed class LoopHarness
    {
        private readonly List<AIChatSessionPrompt> _prompts = [];

        public LoopHarness(bool useRealTurns = false)
        {
            Activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Status = ActivityStatus.AwaitingCustomerAnswer,
                AIProfileId = "profile-1",
                SubjectContentType = "Opportunity",
                Channel = OmnichannelConstants.Channels.Phone,
            };

            Profile = new AIProfile
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
                .ReturnsAsync(() => Profile);

            var completionService = new Mock<IAICompletionService>();
            completionService.Setup(x => x.CompleteAsync(
                    It.IsAny<AIDeployment>(),
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<AICompletionContext>(),
                    It.IsAny<CancellationToken>()))
                .Callback<AIDeployment, IEnumerable<ChatMessage>, AICompletionContext, CancellationToken>((_, messages, context, _) =>
                {
                    Transcript = [.. messages];
                    Completions++;

                    // The context is what carries the tools to the model, so keeping it is how a test can ask
                    // what the model was actually offered on this turn. DuringCompletion stands in for the model
                    // invoking one of them: the real tools record on the scoped turn from inside the completion.
                    CompletionContext = context;
                    DuringCompletion?.Invoke();
                })
                .ReturnsAsync(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)));

            DeploymentManager = new Mock<IAIDeploymentManager>();
            var deploymentManager = DeploymentManager;

            // Realtime is a model capability now, so a profile holds a live call when its chat deployment
            // declares the feature. Off unless a test turns it on with UseRealtime().
            CapabilityService = new Mock<IAIDeploymentCapabilityService>();
            CapabilityService
                .Setup(x => x.GetCapabilities(It.IsAny<AIDeployment>()))
                .Returns(AIDeploymentCapabilities.Empty);
            deploymentManager.Setup(x => x.ResolveSlotAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
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
                .Callback<OmnichannelHandoffRequest, CancellationToken>((_, token) => HandoffToken = token)
                .ReturnsAsync(() => HandoffResult);

            HandoffTurn = new Mock<IOmnichannelHandoffTurn>();
            EndCallTurn = new Mock<IVoiceCallEndTurn>();

            // A journey test uses the real turns: they are the objects the tools record on, and a mock of them
            // cannot show that the thing recording and the thing reading are the same instance.
            RealHandoffTurn = new OmnichannelHandoffTurn();
            RealEndCallTurn = new VoiceCallEndTurn();

            var handoffTurn = useRealTurns ? RealHandoffTurn : HandoffTurn.Object;
            var endCallTurn = useRealTurns ? (IVoiceCallEndTurn)RealEndCallTurn : EndCallTurn.Object;

            CompletionRunner = new RecordingCompletionRunner();
            AbandonmentHandler = new RecordingAbandonmentHandler();

            Loop = new VoiceAgentConversationLoop(
                activityStore.Object,
                sessionManager.Object,
                promptStore.Object,
                completionService.Object,
                handoffTurn,
                endCallTurn,
                CompletionRunner,
                [AbandonmentHandler],
                [CallObserver],
                deploymentManager.Object,
                CapabilityService.Object,
                contextBuilder.Object,
                profileManager.Object,
                FlowSettingsService.Object,
                [HandoffService.Object],
                // Two providers, so the loop has to resolve by name rather than fall back to the only one there is.
                new VoiceAgentMediaProviderResolver([Media, new FakeVoiceAgentMediaProvider("OtherFake")]),
                Realtime,
                SilenceWatchdog,
                Mock.Of<ILiquidTemplateManager>(),
                Mock.Of<IContentManager>(),
                Clock,
                NullLogger<VoiceAgentConversationLoop>.Instance);
        }

        /// <summary>
        /// The loop's clock, which a test moves forward to put time between the moments of a call.
        /// </summary>
        public CrestApps.OrchardCore.Tests.Modules.ContactCenter.AdvanceableClock Clock { get; } = new(new DateTime(2026, 9, 24, 15, 42, 50, DateTimeKind.Utc));

        public OmnichannelActivity Activity { get; }

        public AIProfile Profile { get; }

        public FakeVoiceAgentMediaProvider Media { get; } = new();

        public RecordingRealtimeRunner Realtime { get; } = new();

        /// <summary>
        /// Every listening turn the loop asked to have watched for silence.
        /// </summary>
        public RecordingSilenceWatchdog SilenceWatchdog { get; } = new();

        public VoiceAgentConversationLoop Loop { get; }

        public Mock<IOmnichannelHandoffService> HandoffService { get; }

        public Mock<IOmnichannelHandoffTurn> HandoffTurn { get; }

        public Mock<IVoiceCallEndTurn> EndCallTurn { get; }

        /// <summary>
        /// The real turns, used by the journey test. The transfer tool records on one of these on a live call.
        /// </summary>
        public OmnichannelHandoffTurn RealHandoffTurn { get; }

        public VoiceCallEndTurn RealEndCallTurn { get; }

        public RecordingCompletionRunner CompletionRunner { get; }

        public RecordingAbandonmentHandler AbandonmentHandler { get; }

        public RecordingAutomatedVoiceCallObserver CallObserver { get; } = new();

        public Mock<IAIDeploymentManager> DeploymentManager { get; }

        public Mock<IAIDeploymentCapabilityService> CapabilityService { get; }

        /// <summary>
        /// Gives this profile a chat deployment whose model declares the realtime capability, which is how a call
        /// is held as a live session now that realtime is not a deployment of its own.
        /// </summary>
        public void UseRealtime()
        {
            Profile.ChatDeploymentName = "realtime-deployment";

            var deployment = new AIDeployment { ItemId = "realtime-deployment-1", Name = "realtime-deployment" };

            // Deliberately only through the capability service, and deliberately not through the chat slot: a
            // realtime deployment is excluded from that slot by the framework, because a speech-to-speech model
            // cannot serve a text completion. Setting the chat slot up to hand one back is what let every test
            // here pass while live calls ran the turn-based loop against a correctly configured realtime model.
            CapabilityService
                .Setup(x => x.ResolveDeploymentWithFeatureAsync(
                    AIDeploymentFeatureNames.Realtime,
                    "realtime-deployment",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deployment);
        }

        /// <summary>
        /// The token the enqueue was actually given. The request's own token, already cancelled by the time the
        /// call ends, is what silently dropped the handoff live.
        /// </summary>
        public CancellationToken HandoffToken { get; private set; }

        public Mock<ISubjectFlowSettingsService> FlowSettingsService { get; }

        public SubjectFlowSettings FlowSettings { get; private set; }

        public OmnichannelHandoffResult HandoffResult { get; set; } = OmnichannelHandoffResult.Success();

        public string Reply { get; set; } = "Sure, I can help with that.";

        /// <summary>
        /// The messages the model was given on the last turn.
        /// </summary>
        public List<ChatMessage> Transcript { get; private set; } = [];

        /// <summary>
        /// How many times the model was asked for a turn.
        /// </summary>
        public int Completions { get; private set; }

        /// <summary>
        /// How many turns the transcript holds.
        /// </summary>
        public int PromptCount => _prompts.Count;

        /// <summary>
        /// The completion context the model was given on the last turn.
        /// </summary>
        public AICompletionContext CompletionContext { get; private set; }

        /// <summary>
        /// Runs while the completion is in flight, standing in for the model invoking a tool.
        /// </summary>
        public Action DuringCompletion { get; set; }

        /// <summary>
        /// The tools the model was offered on the last turn.
        /// </summary>
        public IEnumerable<string> OfferedTools =>
            CompletionContext is not null &&
            CompletionContext.AdditionalProperties.TryGetValue(FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey, out var entries) &&
            entries is IEnumerable<ToolRegistryEntry> registry
                ? registry.Select(entry => entry.Name)
                : [];

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
            string providerName = "Fake",
            VoiceAgentAnswerer answerer = VoiceAgentAnswerer.Unknown,
            CancellationToken cancellationToken = default)
            => Loop.HandleAsync(
                new VoiceAgentEvent
                {
                    Kind = kind,
                    ProviderCallId = "call-1",
                    ProviderName = providerName,
                    ActivityId = Activity.ItemId,
                    TranscriptionText = transcript,
                    TranscriptionIsFinal = isFinal,
                    Answerer = answerer,
                },
                cancellationToken);

        /// <summary>
        /// Finishes the call the way the real runner does — on this same loop, but with the request's token out of
        /// the picture, which is the whole point of the seam.
        /// </summary>
        public Task FinishAsync()
            => CompletionRunner.Completion is null
                ? Task.CompletedTask
                : Loop.FinishRealtimeCallAsync(CompletionRunner.Completion);
    }

    /// <summary>
    /// Records what the loop asked to have finished elsewhere, standing in for the child scope the real runner
    /// opens.
    /// </summary>
    /// <summary>
    /// Stands in for the Contact Center, recording the callers it was told had gone.
    /// </summary>
    /// <summary>
    /// Stands in for the Contact Center's audit log, recording the moments of each automated call it was told about.
    /// </summary>
    internal sealed class RecordingAutomatedVoiceCallObserver : IAutomatedVoiceCallObserver
    {
        public List<AutomatedVoiceCallObservation> Observations { get; } = [];

        public Task ObserveAsync(AutomatedVoiceCallObservation observation, CancellationToken cancellationToken = default)
        {
            Observations.Add(observation);

            return Task.CompletedTask;
        }
    }

    internal sealed class RecordingAbandonmentHandler : IQueuedCallerAbandonmentHandler
    {
        public List<string> Released { get; } = [];

        public Task CallerAbandonedAsync(string activityItemId, CancellationToken cancellationToken = default)
        {
            Released.Add(activityItemId);

            return Task.CompletedTask;
        }
    }

    internal sealed class RecordingCompletionRunner : IRealtimeCallCompletionRunner
    {
        public RealtimeCallCompletion Completion { get; private set; }

        /// <summary>
        /// Set by the journey test to the loop that should carry the completion out, standing in for the child
        /// scope the real runner opens — so the test follows the work through instead of stopping at the seam.
        /// </summary>
        public VoiceAgentConversationLoop FinishOn { get; set; }

        public async Task RunAsync(RealtimeCallCompletion completion)
        {
            Completion = completion;

            if (FinishOn is not null)
            {
                await FinishOn.FinishRealtimeCallAsync(completion);
            }
        }
    }
}
