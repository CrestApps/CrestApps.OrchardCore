using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class CallerKeyBufferTests
{
    private static readonly TimeSpan _settle = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task KeysPressedTogether_AreAnsweredOnce()
    {
        // Live: "1234" came as four events within 0.3 seconds, and the AI was asked four times about one digit each.
        var buffer = new CallerKeyBuffer();

        Assert.True(buffer.Add("1"));
        Assert.False(buffer.Add("2"));
        Assert.False(buffer.Add("3"));
        Assert.False(buffer.Add("4"));

        Assert.Equal("1234", await buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KeysReceivedOutOfOrder_AreAnsweredInTheOrderPressed()
    {
        // Each key's webhook is handled on its own request, and those do not always finish in the order the keys
        // were pressed.
        var buffer = new CallerKeyBuffer();
        var pressed = new DateTime(2026, 10, 28, 20, 41, 11, DateTimeKind.Utc);

        buffer.Add("9", pressed.AddMilliseconds(300));
        buffer.Add("8", pressed);
        buffer.Add("1", pressed.AddMilliseconds(600));

        Assert.Equal("891", await buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KeysWithoutATime_KeepTheOrderTheyArrivedIn()
    {
        var buffer = new CallerKeyBuffer();

        buffer.Add("9", new DateTime(2026, 10, 28, 20, 41, 12, DateTimeKind.Utc));
        buffer.Add("8");

        Assert.Equal("98", await buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KeysPressedWhileWaiting_JoinTheSameEntry()
    {
        var buffer = new CallerKeyBuffer();

        Assert.True(buffer.Add("1"));

        var taken = buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Assert.False(buffer.Add("2#"));

        Assert.Equal("12#", await taken);
    }

    [Fact]
    public async Task KeysPressedAfterAnEntryIsTaken_StartANewOne()
    {
        var buffer = new CallerKeyBuffer();

        buffer.Add("1");
        await buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken);

        Assert.True(buffer.Add("9"));
        Assert.Equal("9", await buffer.TakeWhenSettledAsync(_settle, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PromptedResponses_KeepTheSessionsInstructions()
    {
        // Instructions sent with one response replace the session's for it, so a note sent alone lost the persona.
        var instructions = RealtimeVoiceConversationRunner.WithSessionInstructions(
            "You are Sarah from Prestige Auto Group. Speak English.",
            VoiceAgentConversationLoop.KeysPressedTurn("1234"));

        Assert.StartsWith("You are Sarah from Prestige Auto Group. Speak English.", instructions);
        Assert.Contains("1234", instructions);
    }

    [Fact]
    public void PromptedResponses_WithoutSessionInstructions_AreThePromptAlone()
        => Assert.Equal("Say hello.", RealtimeVoiceConversationRunner.WithSessionInstructions(null, "Say hello."));
}
