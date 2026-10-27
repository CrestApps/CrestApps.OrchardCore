using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;

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
}
