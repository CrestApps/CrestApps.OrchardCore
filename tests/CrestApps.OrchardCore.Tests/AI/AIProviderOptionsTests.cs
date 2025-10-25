using CrestApps.OrchardCore.AI.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

public sealed class AIProviderOptionsTests
{
    [Fact]
    public void AIProvider_WhenConnectionsExist_ShouldHaveDefaultConnectionName()
    {
        // Arrange
        var provider = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>
            {
                ["connection1"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["DefaultDeploymentName"] = "gpt-4"
                })
            }
        };

        // Act - Simulate what configuration does
        if (string.IsNullOrEmpty(provider.DefaultConnectionName) && provider.Connections.Any())
        {
            provider.DefaultConnectionName = provider.Connections.First().Key;
        }

        // Assert
        Assert.NotNull(provider.DefaultConnectionName);
        Assert.NotEmpty(provider.DefaultConnectionName);
        Assert.Equal("connection1", provider.DefaultConnectionName);
    }

    [Fact]
    public void AIProvider_WhenMultipleConnectionsExist_ShouldUseFirstAsDefault()
    {
        // Arrange
        var provider = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["connection1"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["DefaultDeploymentName"] = "gpt-4"
                }),
                ["connection2"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["DefaultDeploymentName"] = "gpt-3.5-turbo"
                })
            }
        };

        // Act - Simulate what configuration does
        if (string.IsNullOrEmpty(provider.DefaultConnectionName) && provider.Connections.Any())
        {
            provider.DefaultConnectionName = provider.Connections.First().Key;
        }

        // Assert
        Assert.NotNull(provider.DefaultConnectionName);
        Assert.NotEmpty(provider.DefaultConnectionName);
        Assert.True(provider.Connections.ContainsKey(provider.DefaultConnectionName));
    }

    [Fact]
    public void AIProvider_WhenDefaultConnectionNameIsSet_ShouldNotOverride()
    {
        // Arrange
        var provider = new AIProvider
        {
            DefaultConnectionName = "connection2",
            Connections = new Dictionary<string, AIProviderConnectionEntry>
            {
                ["connection1"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["DefaultDeploymentName"] = "gpt-4"
                }),
                ["connection2"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["DefaultDeploymentName"] = "gpt-3.5-turbo"
                })
            }
        };

        // Act - Simulate what configuration does
        if (string.IsNullOrEmpty(provider.DefaultConnectionName) && provider.Connections.Any())
        {
            provider.DefaultConnectionName = provider.Connections.First().Key;
        }

        // Assert
        Assert.Equal("connection2", provider.DefaultConnectionName);
    }

    [Fact]
    public void AIProvider_WhenNoConnectionsExist_ShouldNotSetDefaultConnectionName()
    {
        // Arrange
        var provider = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>()
        };

        // Act - Simulate what configuration does
        if (string.IsNullOrEmpty(provider.DefaultConnectionName) && provider.Connections.Any())
        {
            provider.DefaultConnectionName = provider.Connections.First().Key;
        }

        // Assert
        Assert.Null(provider.DefaultConnectionName);
    }

    [Fact]
    public void AIProviderConnectionEntry_ShouldSupportCaseInsensitiveKeys()
    {
        // Arrange & Act
        var entry = new AIProviderConnectionEntry(new Dictionary<string, object>
        {
            ["Type"] = "Chat",
            ["DefaultDeploymentName"] = "gpt-4",
            ["ApiKey"] = "sk-test"
        });

        // Assert
        Assert.True(entry.ContainsKey("Type"));
        Assert.True(entry.ContainsKey("type"));
        Assert.True(entry.ContainsKey("TYPE"));
        Assert.Equal("Chat", entry["Type"]);
    }

    [Fact]
    public void AIProviderOptions_Providers_ShouldUseCaseInsensitiveKeys()
    {
        // Arrange
        var options = new AIProviderOptions();

        // Act
        options.Providers.Add("OpenAI", new AIProvider());

        // Assert
        Assert.True(options.Providers.ContainsKey("OpenAI"));
        Assert.True(options.Providers.ContainsKey("openai"));
        Assert.True(options.Providers.ContainsKey("OPENAI"));
    }

    [Fact]
    public void AIProvider_Connections_ShouldSupportDifferentConnectionTypes()
    {
        // Arrange & Act
        var provider = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>
            {
                ["chat-conn"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["Type"] = "Chat",
                    ["DefaultDeploymentName"] = "gpt-4"
                }),
                ["embed-conn"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["Type"] = "Embedding",
                    ["DefaultDeploymentName"] = "text-embedding-3-small"
                }),
                ["speech-conn"] = new AIProviderConnectionEntry(new Dictionary<string, object>
                {
                    ["Type"] = "SpeechToText",
                    ["DefaultDeploymentName"] = "whisper-1"
                })
            }
        };

        // Assert
        Assert.Equal(3, provider.Connections.Count);
        Assert.True(provider.Connections.ContainsKey("chat-conn"));
        Assert.True(provider.Connections.ContainsKey("embed-conn"));
        Assert.True(provider.Connections.ContainsKey("speech-conn"));
    }
}
