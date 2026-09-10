using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What a finished automated call is allowed to claim happened on it.
/// </summary>
/// <remarks>
/// The call review is an LLM reading the transcript, and its answer is written to the CRM as fact for a person to
/// read later. Asked to review a call on which nothing was said, it does not answer "nothing happened" — it writes
/// a fluent account of a conversation that never occurred. That happened on a live call: nobody spoke, and the
/// record read "the customer expressed interest in a vehicle but did not specify the type, budget, or timeline".
/// </remarks>
public sealed class VoiceCallConclusionPolicyTests
{
    [Fact]
    public void ACallHandedToALiveAgent_IsTheAgentsToConclude()
    {
        // Arrange
        // The model's leg ends the instant the caller is passed on, which looks like a hangup from the loop. On a
        // live call, concluding there closed and dispositioned the activity ninety seconds before the agent hung
        // up, and the agent's own wrap-up was then refused because the work was already finished.
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Status = ActivityStatus.InProgress,
            AiEscalated = true,
        };

        // Act & Assert
        Assert.False(VoiceCallConclusionPolicy.ShouldConclude(activity));
    }

    [Fact]
    public void ACallTheModelHandledAlone_IsStillItsToConclude()
    {
        // Arrange
        // The contained case is the whole point of the automation, and must keep writing its own outcome.
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Status = ActivityStatus.InProgress,
            AiEscalated = false,
        };

        // Act & Assert
        Assert.True(VoiceCallConclusionPolicy.ShouldConclude(activity));
    }

    [Fact]
    public void AnActivityAlreadyFinished_IsNotConcludedTwice()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Status = ActivityStatus.Completed,
        };

        // Act & Assert
        Assert.False(VoiceCallConclusionPolicy.ShouldConclude(activity));
    }

    [Fact]
    public void AnActivityThatCouldNotBeRead_IsNotConcluded()
    {
        // Assert
        // Closing a call whose activity could not be loaded would write an outcome against nothing.
        Assert.False(VoiceCallConclusionPolicy.ShouldConclude(null));
    }

    [Fact]
    public void ACallNobodySpokeOn_IsRecordedAsHavingNoConversation()
    {
        // Arrange
        // Rang out, declined, or answered and hung up on: no turns at all.
        var prompts = Array.Empty<AIChatSessionPrompt>();

        // Act
        var hasConversation = VoiceCallConclusionPolicy.HasConversation(prompts);
        var notes = VoiceCallConclusionPolicy.ResolveNotes(hasConversation, modelSummary: null);

        // Assert
        Assert.False(hasConversation);
        Assert.Equal(VoiceCallConclusionPolicy.NoConversationNote, notes);
    }

    [Fact]
    public void ASummaryOfferedForACallNobodySpokeOn_IsNotBelieved()
    {
        // Arrange
        // This is the actual defect. Even handed a confident, detailed summary, a call with no transcript cannot
        // have produced it — so the summary is discarded rather than written to the customer's record.
        var invented = "The customer expressed interest in a vehicle but did not specify the type, budget, or timeline.";

        // Act
        var notes = VoiceCallConclusionPolicy.ResolveNotes(hasConversation: false, modelSummary: invented);

        // Assert
        Assert.Equal(VoiceCallConclusionPolicy.NoConversationNote, notes);
        Assert.DoesNotContain("expressed interest", notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARealConversation_KeepsWhatTheReviewWrote()
    {
        // Arrange
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Is now a good time?" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Sure." },
        };

        // Act
        var hasConversation = VoiceCallConclusionPolicy.HasConversation(prompts);
        var notes = VoiceCallConclusionPolicy.ResolveNotes(hasConversation, "Wants a used SUV under thirty grand.");

        // Assert
        Assert.True(hasConversation);
        Assert.Equal("Wants a used SUV under thirty grand.", notes);
    }

    [Fact]
    public void AConversationTheReviewDidNotSummarize_StillReadsAsACompletedCall()
    {
        // Arrange
        // Something was said but the review returned nothing usable. That is a completed call with no summary,
        // which is a different fact from a call nobody spoke on.
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Not interested, thanks." },
        };

        // Act
        var notes = VoiceCallConclusionPolicy.ResolveNotes(VoiceCallConclusionPolicy.HasConversation(prompts), "   ");

        // Assert
        Assert.Equal(VoiceCallConclusionPolicy.CompletedWithoutSummaryNote, notes);
    }

    [Fact]
    public void TurnsThatTranscribedToNothing_DoNotCountAsSomebodySpeaking()
    {
        // Arrange
        // An empty or whitespace turn is a transcription that produced nothing, not a person talking. Counting it
        // would send the review a transcript with no words in it and invite the same invention.
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "   " },
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = null },
        };

        // Act & Assert
        Assert.False(VoiceCallConclusionPolicy.HasConversation(prompts));
    }

    [Fact]
    public void TheScaffoldingThePlatformAdds_IsNotSomebodySpeaking()
    {
        // Arrange
        // Generated prompts are added by the platform around the conversation; a call carrying only those had
        // nobody on it.
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.System, Content = "You are a sales rep.", IsGeneratedPrompt = true },
        };

        // Act & Assert
        Assert.False(VoiceCallConclusionPolicy.HasConversation(prompts));
    }

    [Fact]
    public void AMissingTranscript_IsTreatedAsNoConversationRatherThanThrowing()
    {
        // Arrange
        // Concluding a call must not fail because the transcript could not be read; the call still needs closing.

        // Act & Assert
        Assert.False(VoiceCallConclusionPolicy.HasConversation(null));
    }
}
