using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Telling a recorded voicemail greeting from a person answering, on text alone.
/// </summary>
public sealed class VoicemailGreetingTests
{
    [Theory]
    [InlineData("When you have finished recording you may hang up.")]
    [InlineData("Please leave a message after the tone.")]
    [InlineData("Hi, you've reached Amani. I can't come to the phone right now.")]
    [InlineData("Hi, you’ve reached Amani.")]
    [InlineData("The person you're calling is not available. At the tone, please record your message.")]
    [InlineData("Your call has been forwarded to an automated voice messaging system.")]
    [InlineData("The Google subscriber you have called is not available.")]
    public void AGreeting_IsRecognised(string text)
    {
        // Act
        var recognised = VoicemailGreeting.IsRecordedGreeting(text);

        // Assert
        Assert.True(recognised);
    }

    [Theory]
    [InlineData("Hello?")]
    [InlineData("Yes, this is Amani.")]
    [InlineData("No thanks, I'm not interested.")]
    [InlineData("Who is this?")]
    [InlineData("Sorry, he's busy, can you call back later?")]
    [InlineData("")]
    [InlineData(null)]
    public void APersonAnswering_IsNotTakenForAGreeting(string text)
    {
        // Act
        var recognised = VoicemailGreeting.IsRecordedGreeting(text);

        // Assert
        Assert.False(recognised);
    }

    [Theory]
    [InlineData("When you have finished recording you may hang up.", true)]
    [InlineData("Please leave a message after the tone.", true)]
    [InlineData("Hi, you've reached Amani.", false)]
    [InlineData("The person you're calling is not available.", false)]
    public void AGreeting_AsksForTheMessage_OnlyOnceItHasReachedThatPoint(string text, bool expected)
    {
        // Act
        var invites = VoicemailGreeting.InvitesTheMessage(text);

        // Assert
        Assert.Equal(expected, invites);
    }

    [Fact]
    public void ATranscript_OpensWithAGreeting_WhenTheFirstThingTheOtherSideSaidWasOne()
    {
        // Arrange
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hi, this is Alex. Do you have a minute?" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "When you have finished recording you may hang up." },
        };

        // Act
        var opens = VoicemailGreeting.OpensWithRecordedGreeting(prompts);

        // Assert
        Assert.True(opens);
    }

    [Fact]
    public void ATranscript_DoesNotOpenWithAGreeting_WhenAPersonAnsweredFirst()
    {
        // Arrange
        // A customer who later mentions voicemail is still a customer.
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hi, this is Alex. Do you have a minute?" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Sure, go ahead." },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "I got your voicemail yesterday." },
        };

        // Act
        var opens = VoicemailGreeting.OpensWithRecordedGreeting(prompts);

        // Assert
        Assert.False(opens);
    }
}
