using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// An automated voice agent tells the person it called that the call is recorded before anything else it says, in
/// the tenant's words. Left to the profile's instructions, the notice was only as reliable as the model's reading of
/// them, and every profile had to repeat it.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    private const string Disclosure = "This call may be recorded for quality assurance and training purposes.";

    [Fact]
    public async Task ATurnBasedCall_SpeaksTheDisclosureWordForWord_AheadOfTheGreeting()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // One line, so nothing can come between the disclosure and the greeting.
        var opening = Assert.Single(harness.Media.Spoken);
        Assert.StartsWith(Disclosure + " ", opening, StringComparison.Ordinal);
        Assert.True(opening.Length > Disclosure.Length + 1);
    }

    [Fact]
    public async Task ATurnBasedCall_WithoutADisclosure_OpensWithTheGreetingAlone()
    {
        // Arrange
        // Call recording is on, but automated voice agents are not told to give the disclosure.
        var harness = new LoopHarness();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(null));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(Disclosure, Assert.Single(harness.Media.Spoken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARealtimeCall_IsGivenTheDisclosureForItsOpening()
    {
        // Arrange
        // A live session writes its own opening, so it can only be told to give the disclosure.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.DisclosureProviders.Add(new FixedDisclosureProvider(Disclosure));

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Disclosure, Assert.Single(harness.Realtime.Sessions).RecordingDisclosure);
    }

    [Fact]
    public async Task ARealtimeCall_WithoutCallRecording_IsGivenNoDisclosure()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Assert.Single(harness.Realtime.Sessions).RecordingDisclosure);
    }

    private sealed class FixedDisclosureProvider : IRecordingDisclosureProvider
    {
        private readonly string _aiDisclosure;

        public FixedDisclosureProvider(string aiDisclosure)
        {
            _aiDisclosure = aiDisclosure;
        }

        public Task<string> GetDisclosureAsync(RecordingDisclosureCallType callType, CancellationToken cancellationToken = default)
            => Task.FromResult(callType == RecordingDisclosureCallType.AIVoiceAgent ? _aiDisclosure : null);
    }
}
