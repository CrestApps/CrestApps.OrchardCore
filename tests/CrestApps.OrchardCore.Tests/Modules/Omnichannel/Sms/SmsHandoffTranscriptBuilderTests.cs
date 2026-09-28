using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// The automated transcript is what the human thread is hydrated from at handoff. Each entry has to carry
/// enough identity for the thread to recognise a message it already holds: the prompt's own id for every turn,
/// and the provider's message id for the customer's texts.
/// </summary>
public sealed class SmsHandoffTranscriptBuilderTests
{
    private static readonly DateTime _at = new(2026, 9, 25, 17, 52, 14, DateTimeKind.Utc);

    [Fact]
    public void CreateCustomerPrompt_RemembersTheProviderMessageId_AcrossPersistence()
    {
        // Arrange
        var message = new OmnichannelMessage
        {
            Content = "Yes",
            IsInbound = true,
            ProviderMessageId = "SM-yes",
        };

        // Act
        var prompt = SmsHandoffTranscript.CreateCustomerPrompt("session-1", message, _at);

        // The prompt store persists the prompt as JSON; the id has to survive the trip.
        var stored = JsonSerializer.Deserialize<AIChatSessionPrompt>(JsonSerializer.Serialize(prompt));
        var transcript = SmsHandoffTranscript.Build([stored]);

        // Assert
        Assert.Equal("session-1", prompt.SessionId);
        Assert.Equal(ChatRole.User, prompt.Role);
        Assert.Equal("Yes", prompt.Content);
        Assert.Equal(_at, prompt.CreatedUtc);

        var entry = Assert.Single(transcript);
        Assert.True(entry.IsInbound);
        Assert.Equal(prompt.ItemId, entry.Id);
        Assert.Equal("SM-yes", entry.ProviderMessageId);
    }

    [Fact]
    public void Build_KeysEveryTurn_ByItsPromptId()
    {
        // Arrange
        var prompts = new[]
        {
            new AIChatSessionPrompt { ItemId = "prompt-1", Role = ChatRole.Assistant, Content = "Hi there", CreatedUtc = _at },
            new AIChatSessionPrompt { ItemId = "prompt-2", Role = ChatRole.User, Content = "Hi", CreatedUtc = _at.AddMinutes(1) },
        };

        // Act
        var transcript = SmsHandoffTranscript.Build(prompts);

        // Assert
        Assert.Equal(["prompt-1", "prompt-2"], transcript.Select(entry => entry.Id).ToArray());
        Assert.Equal([false, true], transcript.Select(entry => entry.IsInbound).ToArray());
        Assert.All(transcript, entry => Assert.Null(entry.ProviderMessageId));
        Assert.Equal(_at, transcript[0].CreatedUtc);
    }
}
