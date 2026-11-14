using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// SEC-P2: keys a caller types while the keypad is private reach the loop on whichever node the provider's keypad
/// webhook lands on. Each node here has its own memory cache, as on a site without the Redis cache, so the node the
/// keys land on has no entry: only the window on the activity, which every node reads, keeps them out of the
/// conversation.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    private const string CardNumber = "4111111111111111";

    [Fact]
    public async Task SEC_P2_OnATurnBasedCall_KeysThatLandOnANodeWithNoEntry_NeverReachTheModel()
    {
        // Arrange: node A started the card entry; this loop runs on node B, whose cache holds nothing.
        var harness = new LoopHarness(storesCopies: true);
        harness.Reply = "Got it.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        var completions = harness.Completions;
        await harness.CreateSecureKeypadNode().StartAsync(harness.Activity.ItemId, SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act: one key per webhook, the way Telnyx sends them.
        foreach (var key in CardNumber)
        {
            await harness.Loop.HandleAsync(KeyPressed(harness, key.ToString()), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(harness.CallerKeys.Collected);
        Assert.Equal(completions, harness.Completions);
        Assert.DoesNotContain(harness.Transcript, message => message.Text?.Contains('4') == true);
        Assert.True(harness.ReadStoredCopy().Has<SecureCaptureWindow>());
    }

    [Fact]
    public async Task SEC_P2_OnALiveCall_KeysThatLandOnANodeWithNoEntry_NeverReachTheSession()
    {
        // Arrange: the live session holding the call takes commands on this node, and the entry was started on node A.
        var harness = new LoopHarness(storesCopies: true);
        var heard = new List<AIVoiceCallCommand>();
        using var session = harness.VoiceCallCommands.Register(harness.Activity.ItemId, command =>
        {
            heard.Add(command);

            return Task.FromResult(true);
        });
        await harness.CreateSecureKeypadNode().StartAsync(harness.Activity.ItemId, SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);

        // Act
        foreach (var key in CardNumber)
        {
            await harness.Loop.HandleAsync(KeyPressed(harness, key.ToString()), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(heard);
        Assert.Empty(harness.CallerKeys.Collected);
    }

    [Fact]
    public async Task SEC_P2_OnceTheWindowHasExpired_KeysAreTheCallersTurnAgain()
    {
        // Arrange
        var harness = new LoopHarness(storesCopies: true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.CreateSecureKeypadNode().StartAsync(harness.Activity.ItemId, SecureCaptureField.CreditCardNumber, TestContext.Current.CancellationToken);
        harness.Clock.Advance(SecureKeypadCaptures.Expiry + TimeSpan.FromSeconds(1));

        // Act
        await harness.Loop.HandleAsync(KeyPressed(harness, "8"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("8", Assert.Single(harness.CallerKeys.Collected).Digits);
    }

    [Fact]
    public async Task SEC_P2_OnTheNodeThatStartedTheEntry_TheEntryCompletes_AndLaterKeysAreTheCallersTurn()
    {
        // Arrange: the same node starts the entry and receives the keys.
        var harness = new LoopHarness(storesCopies: true);
        harness.Reply = "Thanks.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.SecureKeypadCaptures.StartAsync(harness.Activity.ItemId, SecureCaptureField.CardExpiry, TestContext.Current.CancellationToken);

        // Act
        await harness.Loop.HandleAsync(KeyPressed(harness, "1228"), TestContext.Current.CancellationToken);
        await harness.Loop.HandleAsync(KeyPressed(harness, "8"), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.ReadStoredCopy().Has<SecureCaptureWindow>());
        Assert.DoesNotContain(harness.Transcript, message => message.Text?.Contains("1228", StringComparison.Ordinal) == true);
        Assert.Equal("8", Assert.Single(harness.CallerKeys.Collected).Digits);
    }
}
