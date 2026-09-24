using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// How a conversation ended, in the terms the usage report groups by. A live session says what it decided; a
/// turn-based call is read back from what it left on the activity and in the transcript.
/// </summary>
public sealed class AIVoiceSessionOutcomesTests
{
    [Theory]
    [InlineData(true, false, false, false, AIVoiceSessionOutcome.HandedToAgent)]
    [InlineData(true, true, false, true, AIVoiceSessionOutcome.HandedToAgent)]
    [InlineData(false, true, true, false, AIVoiceSessionOutcome.Voicemail)]
    [InlineData(false, true, false, false, AIVoiceSessionOutcome.CompletedByAI)]
    [InlineData(false, false, false, true, AIVoiceSessionOutcome.Failed)]
    [InlineData(false, false, false, false, AIVoiceSessionOutcome.CallerHungUp)]
    public void ALiveSession_EndsTheWayItDecided(bool handoff, bool endCall, bool voicemail, bool failed, AIVoiceSessionOutcome expected)
    {
        // Act
        var outcome = AIVoiceSessionOutcomes.ForRealtime(handoff, endCall, voicemail, failed);

        // Assert
        Assert.Equal(expected, outcome);
    }

    [Fact]
    public void ATurnBasedCall_HandedToAPerson_IsAHandoff()
    {
        // Arrange
        var activity = new OmnichannelActivity { AiEscalated = true, Status = ActivityStatus.InProgress };

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.HandedToAgent, AIVoiceSessionOutcomes.ForTurnBased(activity, Said(), wasAnswered: true));
    }

    [Fact]
    public void ATurnBasedCallNobodyAnswered_IsNoAnswer_EvenOnceTheExpiryPassHasFailedIt()
    {
        // Arrange
        var activity = new OmnichannelActivity { Status = ActivityStatus.Failed };

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.NoAnswer, AIVoiceSessionOutcomes.ForTurnBased(activity, [], wasAnswered: false));
    }

    [Fact]
    public void ATurnBasedCallThatReachedAVoicemail_IsVoicemail()
    {
        // Arrange
        var activity = new OmnichannelActivity { Status = ActivityStatus.Completed };
        activity.Put(new VoicemailReached { MessageLeft = true });

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.Voicemail, AIVoiceSessionOutcomes.ForTurnBased(activity, Said((ChatRole.Assistant, "Hi there.")), wasAnswered: true));
    }

    [Fact]
    public void ATurnBasedCallThatFailed_IsFailed()
    {
        // Arrange
        var activity = new OmnichannelActivity { Status = ActivityStatus.Failed };

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.Failed, AIVoiceSessionOutcomes.ForTurnBased(activity, Said((ChatRole.Assistant, "Hi there.")), wasAnswered: true));
    }

    [Fact]
    public void ATurnBasedCallTheAssistantHungUp_WasCompletedByTheAssistant()
    {
        // Arrange
        // The hangup marker is how the loop knows its last line was the goodbye.
        var activity = new OmnichannelActivity { Status = ActivityStatus.Completed };
        var prompts = Said(
            (ChatRole.Assistant, "Hi there."),
            (ChatRole.User, "Not interested, thanks."),
            (ChatRole.Assistant, "No problem, have a good day. " + VoiceAgentConversationLoop.HangupMarker));

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.CompletedByAI, AIVoiceSessionOutcomes.ForTurnBased(activity, prompts, wasAnswered: true));
    }

    [Fact]
    public void ATurnBasedCallThatEndedMidConversation_WasHungUpByTheCaller()
    {
        // Arrange
        var activity = new OmnichannelActivity { Status = ActivityStatus.Completed };
        var prompts = Said(
            (ChatRole.Assistant, "Hi there."),
            (ChatRole.User, "Who is this?"),
            (ChatRole.Assistant, "It's the dealership calling about your service."));

        // Act & Assert
        Assert.Equal(AIVoiceSessionOutcome.CallerHungUp, AIVoiceSessionOutcomes.ForTurnBased(activity, prompts, wasAnswered: true));
    }

    private static List<AIChatSessionPrompt> Said(params (ChatRole Role, string Content)[] turns)
        => turns.Select(turn => new AIChatSessionPrompt { Role = turn.Role, Content = turn.Content }).ToList();
}
