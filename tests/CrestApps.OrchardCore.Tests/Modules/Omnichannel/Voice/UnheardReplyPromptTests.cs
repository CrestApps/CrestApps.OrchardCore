using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What the assistant is asked to say when the caller's answer went unheard.
/// </summary>
/// <remarks>
/// Live, the assistant read an email address back, the caller's "yes" was never reported, and asked only to "say it
/// again" the model asked for the whole address -- the caller answered that they had already given it.
/// </remarks>
public sealed class UnheardReplyPromptTests
{
    [Fact]
    public void ThePrompt_CarriesTheQuestionTheCallerWasAnswering()
    {
        var prompt = RealtimeVoiceConversationRunner.UnheardReplyPrompt("Okay, mike at crestapps dot com — did I get that right?");

        Assert.Contains("did I get that right?", prompt, StringComparison.Ordinal);
        Assert.Contains("was that a yes?", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not ask them to repeat anything they told you earlier", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNothingSaidYet_ThePromptStillAsksForTheAnswerOnly()
    {
        var prompt = RealtimeVoiceConversationRunner.UnheardReplyPrompt(null);

        Assert.DoesNotContain("Your last words", prompt, StringComparison.Ordinal);
        Assert.Contains("was that a yes?", prompt, StringComparison.Ordinal);
    }
}
