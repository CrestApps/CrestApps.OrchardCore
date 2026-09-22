using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class EndCallToolTests
{
    [Fact]
    public async Task AnEndedCall_IsRecordedWithItsReason_AndIsNotVoicemailUnlessTheModelSaysSo()
    {
        // Arrange
        var (tool, turn, arguments) = Create();
        arguments["reason"] = "customer has what they needed";

        // Act
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(turn.EndCallRequested);
        Assert.Equal("customer has what they needed", turn.Reason);
        Assert.False(turn.ReachedVoicemail);
    }

    [Theory]
    [InlineData("bool")]
    [InlineData("json")]
    [InlineData("text")]
    public async Task ACallEndedOnVoicemail_IsRecordedAsVoicemail_HoweverTheArgumentArrives(string form)
    {
        // Arrange
        // The tool is not strict, so a model can send the flag as a JSON boolean, a boxed boolean or the word.
        // Missing it would put a person's moment to answer back at the end of the recording.
        var (tool, turn, arguments) = Create();
        arguments["reason"] = "left a voicemail message";
        arguments["voicemail"] = form switch
        {
            "bool" => true,
            "json" => JsonSerializer.SerializeToElement(true),
            _ => "true",
        };

        // Act
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(turn.EndCallRequested);
        Assert.True(turn.ReachedVoicemail);
    }

    [Fact]
    public void TheModelIsToldToLeaveOneMessageAfterTheToneAndEndTheCallStraightAway()
    {
        // Arrange
        // Live, the assistant left its message over the greeting before the tone, waited for a reply that was
        // never coming, and repeated itself -- the recording kept running on silence throughout.
        var tool = new EndCallTool();

        // Assert
        Assert.Contains("voicemail", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tone", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not repeat", tool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"voicemail\"", tool.JsonSchema.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void AReset_ClearsTheVoicemailDecision_ForTheCallThatFollows()
    {
        // Arrange
        var turn = new VoiceCallEndTurn();
        turn.RequestEndCall("left a voicemail message", reachedVoicemail: true);

        // Act
        turn.Reset();

        // Assert
        Assert.False(turn.ReachedVoicemail);
        Assert.False(turn.EndCallRequested);
    }

    private static (EndCallTool Tool, VoiceCallEndTurn Turn, AIFunctionArguments Arguments) Create()
    {
        var turn = new VoiceCallEndTurn();
        var services = new ServiceCollection()
            .AddSingleton<IVoiceCallEndTurn>(turn)
            .BuildServiceProvider();

        return (new EndCallTool(), turn, new AIFunctionArguments { Services = services });
    }
}
