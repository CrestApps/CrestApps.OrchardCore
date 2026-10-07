using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The transcript the handoff summary is written from: what the customer heard, one line per turn.
/// </summary>
public sealed class VoiceHandoffTranscriptTests
{
    [Fact]
    public void BuildHandoffTranscript_LabelsEachSpeaker_AndDropsTheHangupMarker()
    {
        // Arrange
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Hi, is now a good time?" },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Yes, I want a Tesla." },
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "Connecting you now. " + VoiceAgentConversationLoop.HangupMarker },
        };

        // Act
        var transcript = VoiceAgentConversationLoop.BuildHandoffTranscript(prompts);

        // Assert
        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "Assistant: Hi, is now a good time?",
                "Customer: Yes, I want a Tesla.",
                "Assistant: Connecting you now."),
            transcript);
    }

    [Fact]
    public void BuildHandoffTranscript_LeavesOutGeneratedAndEmptyTurns()
    {
        // Arrange
        var prompts = new[]
        {
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Instructions for the model", IsGeneratedPrompt = true },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "   " },
            new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = VoiceAgentConversationLoop.HangupMarker },
            new AIChatSessionPrompt { Role = ChatRole.User, Content = "Talk to an agent." },
        };

        // Act
        var transcript = VoiceAgentConversationLoop.BuildHandoffTranscript(prompts);

        // Assert
        Assert.Equal("Customer: Talk to an agent.", transcript);
    }

    [Fact]
    public void BuildHandoffTranscript_WithNothingSaid_IsEmpty()
    {
        Assert.Equal(string.Empty, VoiceAgentConversationLoop.BuildHandoffTranscript(null));
        Assert.Equal(string.Empty, VoiceAgentConversationLoop.BuildHandoffTranscript([]));
    }
}
