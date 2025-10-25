using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

public sealed class AIProviderConnectionExtensionsTests
{
    [Fact]
    public void GetConnectionType_WhenTypeIsChat_ShouldReturnChat()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = "Chat"
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Chat, result);
    }

    [Fact]
    public void GetConnectionType_WhenTypeIsEmbedding_ShouldReturnEmbedding()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = "Embedding"
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Embedding, result);
    }

    [Fact]
    public void GetConnectionType_WhenTypeIsSpeechToText_ShouldReturnSpeechToText()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = "SpeechToText"
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.SpeechToText, result);
    }

    [Theory]
    [InlineData("chat")]
    [InlineData("CHAT")]
    [InlineData("Chat")]
    public void GetConnectionType_WhenTypeIsCaseInsensitive_ShouldReturnChat(string typeValue)
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = typeValue
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Chat, result);
    }

    [Fact]
    public void GetConnectionType_WhenTypeIsNull_ShouldReturnChatAsDefault()
    {
        // Arrange
        var entry = new Dictionary<string, object>();

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Chat, result);
    }

    [Fact]
    public void GetConnectionType_WhenTypeIsInvalid_ShouldReturnChatAsDefault()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = "InvalidType"
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Chat, result);
    }

    [Fact]
    public void GetConnectionType_WhenTypeIsEmpty_ShouldReturnChatAsDefault()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Type"] = ""
        };

        // Act
        var result = entry.GetConnectionType();

        // Assert
        Assert.Equal(AIProviderConnectionType.Chat, result);
    }

    [Fact]
    public void GetDefaultDeploymentName_WhenValueExists_ShouldReturnValue()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["DefaultDeploymentName"] = "gpt-4"
        };

        // Act
        var result = entry.GetDefaultDeploymentName();

        // Assert
        Assert.Equal("gpt-4", result);
    }

    [Fact]
    public void GetDefaultDeploymentName_WhenValueDoesNotExist_ShouldThrowException()
    {
        // Arrange
        var entry = new Dictionary<string, object>();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => entry.GetDefaultDeploymentName());
        Assert.Contains("DefaultDeploymentName", exception.Message);
    }

    [Fact]
    public void GetDefaultDeploymentName_WhenValueDoesNotExistAndThrowIsFalse_ShouldReturnNull()
    {
        // Arrange
        var entry = new Dictionary<string, object>();

        // Act
        var result = entry.GetDefaultDeploymentName(throwException: false);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetDefaultEmbeddingDeploymentName_WhenValueExists_ShouldReturnValue()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["DefaultEmbeddingDeploymentName"] = "text-embedding-3-small"
        };

        // Act
        var result = entry.GetDefaultEmbeddingDeploymentName();

        // Assert
        Assert.Equal("text-embedding-3-small", result);
    }

    [Fact]
    public void GetDefaultSpeechToTextDeploymentName_WhenValueExists_ShouldReturnValue()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["DefaultSpeechToTextDeploymentName"] = "whisper-1"
        };

        // Act
        var result = entry.GetDefaultSpeechToTextDeploymentName();

        // Assert
        Assert.Equal("whisper-1", result);
    }

    [Fact]
    public void GetApiKey_WhenValueExists_ShouldReturnValue()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["ApiKey"] = "sk-test-key"
        };

        // Act
        var result = entry.GetApiKey();

        // Assert
        Assert.Equal("sk-test-key", result);
    }

    [Fact]
    public void GetEndpoint_WhenValueExists_ShouldReturnUri()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Endpoint"] = "https://api.openai.com/v1"
        };

        // Act
        var result = entry.GetEndpoint();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://api.openai.com/v1", result.ToString().TrimEnd('/'));
    }

    [Fact]
    public void GetEndpoint_WhenValueIsInvalidAndThrowIsFalse_ShouldReturnNull()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["Endpoint"] = "not-a-valid-uri"
        };

        // Act
        var result = entry.GetEndpoint(throwException: false);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetStringValue_WhenValueIsJsonElement_ShouldReturnString()
    {
        // Arrange
        var jsonElement = JsonDocument.Parse("\"test-value\"").RootElement;
        var entry = new Dictionary<string, object>
        {
            ["TestKey"] = jsonElement
        };

        // Act
        var result = entry.GetStringValue("TestKey");

        // Assert
        Assert.Equal("test-value", result);
    }

    [Fact]
    public void GetBooleanOrFalseValue_WhenValueIsTrue_ShouldReturnTrue()
    {
        // Arrange
        var entry = new Dictionary<string, object>
        {
            ["TestBool"] = true
        };

        // Act
        var result = entry.GetBooleanOrFalseValue("TestBool");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetBooleanOrFalseValue_WhenValueDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var entry = new Dictionary<string, object>();

        // Act
        var result = entry.GetBooleanOrFalseValue("TestBool");

        // Assert
        Assert.False(result);
    }
}
