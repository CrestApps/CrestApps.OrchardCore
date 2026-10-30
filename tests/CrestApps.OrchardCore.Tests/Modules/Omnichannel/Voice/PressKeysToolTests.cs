using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class PressKeysToolTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("press 2 then #", "2#")]
    [InlineData("W204#", "W204#")]
    [InlineData("*9w1", "*9w1")]
    [InlineData("abc", "")]
    [InlineData(null, "")]
    public void Sanitize_KeepsOnlyKeypadKeysAndPauses(string requested, string expected)
        => Assert.Equal(expected, PressKeysTool.Sanitize(requested));

    [Fact]
    public void KeysPressedTurn_TellsTheAIWhatTheCallerPressed()
        => Assert.Contains("123#", VoiceAgentConversationLoop.KeysPressedTurn("123#"));

    [Fact]
    public void TheModelIsToldToPressOneKeyOnce()
    {
        // Live, the model pressed the key it needed and then the key for hearing the menu again.
        Assert.Contains("the one option you need, once", new PressKeysTool().Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PressingKeys_TellsTheCallItHasReachedAPhoneMenu()
    {
        // Arrange
        // A live session holds the call in another request, and only the signal reaches it.
        var call = new ToolCall(isRealtime: true);

        // Act
        await call.PressAsync("1");

        // Assert
        Assert.Equal("1", Assert.Single(call.Media.KeysPressed));
        Assert.True(call.Signals.Watch("Fake", "call-1").PhoneMenuReached.IsCancellationRequested);
    }

    [Fact]
    public async Task PressingKeys_OnATurnBasedCall_TakesTheVoicemailMarkOff()
    {
        // Arrange
        // Live, the menu was taken for a voicemail before the key was pressed, and a message was left on it.
        var call = new ToolCall(isRealtime: false);
        call.Activity.Put(new VoicemailReached { DetectedByProvider = true });

        // Act
        await call.PressAsync("1");

        // Assert
        Assert.False(call.Activity.TryGet<VoicemailReached>(out _));
        call.Store.Verify(x => x.UpdateAsync(call.Activity, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PressingKeys_AfterTheVoicemailMessageWasLeft_KeepsTheMark()
    {
        // Arrange
        var call = new ToolCall(isRealtime: false);
        call.Activity.Put(new VoicemailReached { DetectedByProvider = true, MessageLeft = true });

        // Act
        await call.PressAsync("1");

        // Assert
        Assert.True(call.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.MessageLeft);
    }

    private sealed class ToolCall
    {
        public ToolCall(bool isRealtime)
        {
            Activity = new OmnichannelActivity { ItemId = "activity-1" };
            Activity.Put(new AILiveCall { ProviderName = "Fake", ProviderCallId = "call-1", IsRealtime = isRealtime });

            Store.Setup(x => x.FindByIdAsync("activity-1", It.IsAny<CancellationToken>())).ReturnsAsync(Activity);
        }

        public OmnichannelActivity Activity { get; }

        public Mock<IOmnichannelActivityStore> Store { get; } = new();

        public FakeVoiceAgentMediaProvider Media { get; } = new();

        public AnsweringMachineSignals Signals { get; } = new(new AdvanceableClock(new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc)));

        public async Task PressAsync(string keys)
        {
            var services = new ServiceCollection()
                .AddSingleton(Store.Object)
                .AddSingleton<IVoiceAgentMediaProviderResolver>(new VoiceAgentMediaProviderResolver([Media]))
                .AddSingleton<IAnsweringMachineSignals>(Signals)
                .BuildServiceProvider();

            using var scope = AIConversationScope.Begin(new AIConversationContext { ActivityId = "activity-1" });

            await new PressKeysTool().InvokeAsync(
                new AIFunctionArguments { ["keys"] = keys, Services = services },
                TestContext.Current.CancellationToken);
        }
    }
}
