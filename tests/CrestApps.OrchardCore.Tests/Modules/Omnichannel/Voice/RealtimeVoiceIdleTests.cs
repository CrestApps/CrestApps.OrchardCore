using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What a live call does when the line goes quiet: it asks twice whether the caller is still there, then says goodbye
/// and ends.
/// </summary>
/// <remarks>
/// Live, a session that had asked twice stopped asking and held the line open, unheard, for five minutes; and asked to
/// continue the conversation, the model filled a silent line by talking for the customer ("I'm looking to schedule a
/// haircut appointment").
/// </remarks>
public sealed class RealtimeVoiceIdleTests
{
    [Fact]
    public void AShortSilence_IsLeftAlone()
    {
        // Act
        var step = RealtimeVoiceConversationRunner.NextIdleStep(0, RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp - TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.Wait, step);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ASilence_IsBrokenByAskingIfTheCallerIsStillThere(int promptsAsked)
    {
        // Act
        var step = RealtimeVoiceConversationRunner.NextIdleStep(promptsAsked, RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp);

        // Assert
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.AskIfStillThere, step);
    }

    [Fact]
    public void ASilenceAfterEveryPrompt_EndsTheCallWithAGoodbye()
    {
        // Act
        var step = RealtimeVoiceConversationRunner.NextIdleStep(
            RealtimeVoiceConversationRunner.MaximumIdlePrompts,
            RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp);

        // Assert
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.SayGoodbye, step);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(RealtimeVoiceConversationRunner.MaximumIdlePrompts)]
    public void ASilenceWhileAResponseIsInFlight_IsLeftAlone(int promptsAsked)
    {
        // Arrange
        // A standalone message may only be asked for when no response is in flight; one on top of another is two
        // replies talking over each other.
        var quietFor = RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp + RealtimeVoiceConversationRunner.IdleResponseInFlightLimit - TimeSpan.FromMilliseconds(1);

        // Act
        var step = RealtimeVoiceConversationRunner.NextIdleStep(promptsAsked, quietFor, responseInFlight: true);

        // Assert
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.Wait, step);
    }

    [Fact]
    public void AResponseThatNeverFinishes_DoesNotHoldASilentLineOpen()
    {
        // Arrange
        var quietFor = RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp + RealtimeVoiceConversationRunner.IdleResponseInFlightLimit;

        // Act
        var asked = RealtimeVoiceConversationRunner.NextIdleStep(0, quietFor, responseInFlight: true);
        var ended = RealtimeVoiceConversationRunner.NextIdleStep(RealtimeVoiceConversationRunner.MaximumIdlePrompts, quietFor, responseInFlight: true);

        // Assert
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.AskIfStillThere, asked);
        Assert.Equal(RealtimeVoiceConversationRunner.IdleStep.SayGoodbye, ended);
    }

    [Fact]
    public void BothKindsOfCall_WaitTheSameEightSecondsBeforeEachStep()
    {
        // Arrange
        // Twelve seconds felt like dead air on a phone and kept a silent call open for some forty seconds. A caller
        // meets the same pace whichever model holds the call.
        var expected = TimeSpan.FromSeconds(8);

        // Assert
        Assert.Equal(expected, RealtimeVoiceConversationRunner.IdleBeforeSpeakingUp);
        Assert.Equal(expected, TurnBasedSilenceWatchdog.SilenceBeforeSpeakingUp);
    }

    [Fact]
    public void TheStillThereQuestion_KeepsTheCallsLanguage_AndForbidsSpeakingForTheCaller()
    {
        // Act
        var prompt = RealtimeVoiceConversationRunner.StillTherePrompt("Καλησπέρα, πώς μπορώ να βοηθήσω;");

        // Assert
        Assert.Contains("still there", prompt, StringComparison.Ordinal);
        Assert.Contains("do not speak for them", prompt, StringComparison.Ordinal);
        Assert.Contains("partway through this call", prompt, StringComparison.Ordinal);
        Assert.Contains("\"Καλησπέρα, πώς μπορώ να βοηθήσω;\"", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TheGoodbye_KeepsTheCallsLanguage()
    {
        // Act
        var prompt = RealtimeVoiceConversationRunner.SilentLineGoodbyePrompt("Hello, how can I help?");

        // Assert
        Assert.Contains("goodbye", prompt, StringComparison.Ordinal);
        Assert.Contains("do not repeat any notice", prompt, StringComparison.Ordinal);
        Assert.Contains("\"Hello, how can I help?\"", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutALineYet_ThePromptsStillAsk()
    {
        // Act
        var stillThere = RealtimeVoiceConversationRunner.StillTherePrompt(null);
        var goodbye = RealtimeVoiceConversationRunner.SilentLineGoodbyePrompt("  ");

        // Assert
        Assert.DoesNotContain("quoted only", stillThere, StringComparison.Ordinal);
        Assert.DoesNotContain("quoted only", goodbye, StringComparison.Ordinal);
    }
}
