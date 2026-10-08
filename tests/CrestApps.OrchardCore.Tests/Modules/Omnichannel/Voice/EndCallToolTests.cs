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

    [Theory]
    [InlineData("Okay, let me read that back to make sure I caught it correctly.")]
    [InlineData("So that's mike at example dot com -- did I get that right?")]
    public async Task EndingTheCall_WhileTheCustomerHasNotAnswered_IsRefused(string line)
    {
        // Arrange
        // Live, a model announced a read-back, ended the call in the same breath, then read the address back,
        // asked "did I get that right?" and thanked the customer for a "yes" they never gave.
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid(line);
        arguments["reason"] = "email captured";

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(turn.EndCallRequested);
        Assert.Contains("NOT been ended", result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndingTheCall_AfterTheCustomerAnswered_IsAllowed()
    {
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid("Did I get that right?");
        turn.CustomerAnswered();
        turn.AssistantSaid("Perfect, thanks. Have a great day!");

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.True(turn.EndCallRequested);
    }

    [Fact]
    public async Task AGoodbyeSaidStraightAfterTheQuestion_DoesNotAnswerIt()
    {
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid("Did I get that right?");
        turn.AssistantSaid("Thanks, I'll send those over. Take care!");

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.False(turn.EndCallRequested);
    }

    [Fact]
    public async Task AModelThatKeepsAsking_CanStillEndTheCall()
    {
        // Held twice for the same unanswered question, and then the model is believed: a call must not be kept
        // open for ever by a model that keeps ending it.
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid("Did I get that right?");

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);
        Assert.False(turn.EndCallRequested);

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);
        Assert.True(turn.EndCallRequested);
    }

    [Fact]
    public async Task AVoicemailMessage_EndingInAQuestion_IsNeverHeld()
    {
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid("Could you give us a call back when you have a moment?");
        arguments["voicemail"] = true;

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.True(turn.EndCallRequested);
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

    [Fact]
    public async Task EndingTheCall_OnTheCustomersLastWords_AsksForAGoodbyeFirst()
    {
        // Arrange
        // Live, the customer confirmed their email with "yes", the model answered with the end-call tool alone, was
        // told to say nothing further, and the line went dead four seconds later with no goodbye.
        var (tool, turn, arguments) = Create();
        turn.AssistantSaid("I heard: mike at gmail dot com. Did I get that right?");
        turn.CustomerAnswered();
        arguments["reason"] = "got basics and confirmed email";

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(turn.EndCallRequested);
        Assert.Contains("you have not said goodbye", result?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Say nothing further", result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndingTheCall_AfterTheGoodbye_AsksForNothingMore()
    {
        // Arrange
        var (tool, turn, arguments) = Create();
        turn.CustomerAnswered();
        turn.AssistantSaid("Perfect, thanks Haneen. Have a great day!");

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(turn.EndCallRequested);
        Assert.Contains("Say nothing further", result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndingTheCall_OnVoicemail_NeverAsksForAGoodbye()
    {
        // Arrange
        // The greeting is transcribed as the customer speaking, but the message left on it is the closing line.
        var (tool, turn, arguments) = Create();
        turn.CustomerAnswered();
        arguments["voicemail"] = true;

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(turn.ReachedVoicemail);
        Assert.Contains("Say nothing further", result?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AReset_ForgetsWhoSpokeLast()
    {
        // Arrange
        var turn = new VoiceCallEndTurn();
        turn.CustomerAnswered();

        // Act
        turn.Reset();

        // Assert
        Assert.False(turn.ClosingLineOwed);
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
