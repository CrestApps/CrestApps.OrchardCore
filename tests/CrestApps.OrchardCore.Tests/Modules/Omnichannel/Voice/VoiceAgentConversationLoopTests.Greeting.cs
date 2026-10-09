using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What a turn-based call opens with when its profile has no opening line of its own.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task AProfileWithNoOpeningLine_OpensWithAGenericGreeting_NamingNoBusiness()
    {
        // Arrange
        // The fallback used to introduce a named person from a named dealership, so every tenant whose profile
        // left the opening empty called its customers as somebody else's company.
        var harness = new LoopHarness();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hi there, do you have a quick minute?", Assert.Single(harness.Media.Spoken));
    }
}
