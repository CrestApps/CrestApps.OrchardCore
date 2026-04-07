using System.Text.Json;
using CrestApps.OrchardCore.AI.Chat.Interactions.Hubs;

namespace CrestApps.OrchardCore.Tests.OrchardCore.AI.ChatInteractions;

public sealed class ChatInteractionSettingsValidatorTests
{
    [Fact]
    public void Validate_WhenStrictnessIsTooLarge_ShouldReturnStrictness()
    {
        using var json = JsonDocument.Parse("""
            {
              "strictness": 8
            }
            """);

        var result = ChatInteractionSettingsValidator.Validate(json.RootElement);

        Assert.Equal("strictness", result);
    }

    [Fact]
    public void Validate_WhenSettingsAreWithinRange_ShouldReturnNull()
    {
        using var json = JsonDocument.Parse("""
            {
              "strictness": 5,
              "topNDocuments": 20,
              "temperature": 1.5,
              "topP": 0.8,
              "frequencyPenalty": 1,
              "presencePenalty": 1,
              "pastMessagesCount": 12,
              "maxTokens": 128
            }
            """);

        var result = ChatInteractionSettingsValidator.Validate(json.RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_WhenTopPIsNegative_ShouldReturnTopP()
    {
        using var json = JsonDocument.Parse("""
            {
              "topP": -0.1
            }
            """);

        var result = ChatInteractionSettingsValidator.Validate(json.RootElement);

        Assert.Equal("topP", result);
    }
}
