using CrestApps.OrchardCore.AI.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

public sealed class AIProviderConnectionTypeTests
{
    [Fact]
    public void AIProviderConnectionType_ShouldHaveCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(0, (int)AIProviderConnectionType.Chat);
        Assert.Equal(1, (int)AIProviderConnectionType.Embedding);
        Assert.Equal(2, (int)AIProviderConnectionType.SpeechToText);
    }

    [Theory]
    [InlineData(AIProviderConnectionType.Chat, "Chat")]
    [InlineData(AIProviderConnectionType.Embedding, "Embedding")]
    [InlineData(AIProviderConnectionType.SpeechToText, "SpeechToText")]
    public void AIProviderConnectionType_ToString_ShouldReturnCorrectName(
        AIProviderConnectionType type,
        string expected)
    {
        // Act
        var result = type.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Chat", AIProviderConnectionType.Chat)]
    [InlineData("Embedding", AIProviderConnectionType.Embedding)]
    [InlineData("SpeechToText", AIProviderConnectionType.SpeechToText)]
    [InlineData("chat", AIProviderConnectionType.Chat)]
    [InlineData("embedding", AIProviderConnectionType.Embedding)]
    [InlineData("speechtotext", AIProviderConnectionType.SpeechToText)]
    public void AIProviderConnectionType_Parse_ShouldReturnCorrectValue(
        string input,
        AIProviderConnectionType expected)
    {
        // Act
        var result = Enum.Parse<AIProviderConnectionType>(input, ignoreCase: true);

        // Assert
        Assert.Equal(expected, result);
    }
}
