using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A turn-based call handed to another AI profile by a tool the model called on its turn.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task ACallHandedToAnotherProfileOnThisTurn_IsAnsweredByTheNewProfileStraightAway()
    {
        // Arrange
        // Live, the old profile said it was connecting the caller, and then nobody spoke until the caller did.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        var completionsBefore = harness.Completions;

        harness.DuringCompletion = () =>
        {
            if (harness.Completions == completionsBefore + 1)
            {
                // The switch tool runs inside the old profile's turn.
                harness.Activity.AIProfileId = "profile-2";
                harness.Reply = "Let me connect you with our service desk.";
            }
            else
            {
                harness.Reply = "Hi, this is Alex from the service department. What does your car need?";
            }
        };

        var spokenBefore = harness.Media.Spoken.Count;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I need to book a service for my car.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // The new profile is asked for a turn of its own, told the call has just been handed to it.
        Assert.Equal(completionsBefore + 2, harness.Completions);
        Assert.Contains(harness.Transcript, message => message.Text?.Contains(VoiceAgentConversationLoop.ProfileSwitchedTurn, StringComparison.Ordinal) == true);

        // Only the new profile's greeting is spoken; the old profile's line about connecting is dropped.
        var spoken = Assert.Single(harness.Media.Spoken.Skip(spokenBefore));
        Assert.Equal("Hi, this is Alex from the service department. What does your car need?", spoken);
    }

    [Fact]
    public async Task ATurnThatKeepsItsProfile_IsAnsweredOnce()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        var completionsBefore = harness.Completions;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "I need to book a service for my car.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(completionsBefore + 1, harness.Completions);
        Assert.DoesNotContain(harness.Transcript, message => message.Text?.Contains(VoiceAgentConversationLoop.ProfileSwitchedTurn, StringComparison.Ordinal) == true);
    }
}
