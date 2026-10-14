using CrestApps.Core;
using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Interactions;

public sealed class ChatInteractionClonerTests
{
    [Fact]
    public void CopySettings_WithDataSource_KeepsTheDataSourceAndRetrievalSettings()
    {
        // Arrange
        var source = new ChatInteraction { ItemId = "source" };
        source.Put(new DataSourceMetadata { DataSourceId = "data-source-1" });
        source.Put(new AIDataSourceRagMetadata
        {
            Filter = "category eq 'docs'",
            Strictness = 4,
            TopNDocuments = 7,
            IsInScope = true,
            ObjectTypes = ["text", "table"],
        });

        var target = new ChatInteraction { ItemId = "target" };

        // Act
        ChatInteractionCloner.CopySettings(source, target);

        // Assert
        Assert.True(target.TryGet<DataSourceMetadata>(out var dataSourceMetadata));
        Assert.Equal("data-source-1", dataSourceMetadata.DataSourceId);

        Assert.True(target.TryGet<AIDataSourceRagMetadata>(out var ragMetadata));
        Assert.Equal("category eq 'docs'", ragMetadata.Filter);
        Assert.Equal(4, ragMetadata.Strictness);
        Assert.Equal(7, ragMetadata.TopNDocuments);
        Assert.True(ragMetadata.IsInScope);
        Assert.Equal(["text", "table"], ragMetadata.ObjectTypes);
    }

    [Fact]
    public void CopySettings_WithToolInstancesParametersAndPrompts_KeepsThem()
    {
        // Arrange
        var source = new ChatInteraction { ItemId = "source" };
        source.Put(new AIToolInstanceMetadata { ToolInstanceNames = ["knowledge-base"] });
        source.Put(new AIDeploymentParametersMetadata
        {
            Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["reasoning_effort"] = "low",
            },
        });
        source.Put(new PromptTemplateMetadata
        {
            Templates =
            [
                new PromptTemplateSelectionEntry { TemplateId = "template-1" },
            ],
        });

        var target = new ChatInteraction { ItemId = "target" };

        // Act
        ChatInteractionCloner.CopySettings(source, target);

        // Assert
        Assert.Equal(["knowledge-base"], target.GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames);
        Assert.Equal("low", target.GetOrCreate<AIDeploymentParametersMetadata>().GetValue("reasoning_effort"));
        Assert.Equal("template-1", Assert.Single(target.GetOrCreate<PromptTemplateMetadata>().Templates).TemplateId);
    }

    [Fact]
    public void CopySettings_WithModelSettings_KeepsThemAndTheDocumentIndex()
    {
        // Arrange
        var source = new ChatInteraction
        {
            ItemId = "source",
            ChatDeploymentName = "gpt",
            ConnectionName = "default",
            SystemMessage = "Be brief.",
            Temperature = 0.2f,
            TopP = 0.9f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.3f,
            MaxTokens = 500,
            PastMessagesCount = 6,
            ToolNames = ["tool-a"],
            McpConnectionIds = ["mcp-1"],
            Documents = [new ChatDocumentInfo { DocumentId = "document-1" }],
        };

        var target = new ChatInteraction { ItemId = "target" };

        // Act
        ChatInteractionCloner.CopySettings(source, target);

        // Assert
        Assert.Equal("gpt", target.ChatDeploymentName);
        Assert.Equal("default", target.ConnectionName);
        Assert.Equal("Be brief.", target.SystemMessage);
        Assert.Equal(0.2f, target.Temperature);
        Assert.Equal(0.9f, target.TopP);
        Assert.Equal(0.1f, target.FrequencyPenalty);
        Assert.Equal(0.3f, target.PresencePenalty);
        Assert.Equal(500, target.MaxTokens);
        Assert.Equal(6, target.PastMessagesCount);
        Assert.Equal(["tool-a"], target.ToolNames);
        Assert.Equal(["mcp-1"], target.McpConnectionIds);
        Assert.Equal("document-1", Assert.Single(target.Documents).DocumentId);
        Assert.Equal(1, target.DocumentIndex);
        Assert.Equal("target", target.ItemId);
    }

    [Fact]
    public void CopySettings_WithCopilotSession_DoesNotCopyTheAccessTokens()
    {
        // Arrange
        var source = new ChatInteraction { ItemId = "source" };
        source.Put(new CopilotSessionMetadata
        {
            GitHubUsername = "owner",
            ProtectedAccessToken = "protected-access-token",
            ProtectedRefreshToken = "protected-refresh-token",
        });

        var target = new ChatInteraction { ItemId = "target" };

        // Act
        ChatInteractionCloner.CopySettings(source, target);

        // Assert
        Assert.False(target.Has<CopilotSessionMetadata>());
    }

    [Fact]
    public void CopySettings_ChangingTheCopy_DoesNotChangeTheSource()
    {
        // Arrange
        var source = new ChatInteraction { ItemId = "source" };
        source.Put(new AIDataSourceRagMetadata { Filter = "category eq 'docs'" });

        var target = new ChatInteraction { ItemId = "target" };

        ChatInteractionCloner.CopySettings(source, target);

        // Act
        target.Alter<AIDataSourceRagMetadata>(metadata => metadata.Filter = "category eq 'news'");

        // Assert
        Assert.Equal("category eq 'docs'", source.GetOrCreate<AIDataSourceRagMetadata>().Filter);
        Assert.Equal("category eq 'news'", target.GetOrCreate<AIDataSourceRagMetadata>().Filter);
    }

    [Fact]
    public void CopySettings_WithoutMetadata_AddsNoProperties()
    {
        // Arrange
        var source = new ChatInteraction { ItemId = "source" };
        var target = new ChatInteraction { ItemId = "target" };

        // Act
        ChatInteractionCloner.CopySettings(source, target);

        // Assert
        Assert.Empty(target.Properties);
    }
}
