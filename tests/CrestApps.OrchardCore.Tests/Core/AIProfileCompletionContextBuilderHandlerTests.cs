using CrestApps.Core;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core;

public sealed class AIProfileCompletionContextBuilderHandlerTests
{
    [Fact]
    public async Task BuildingAsync_ProfileMetadataStoredInProperties_PopulatesCompletionContext()
    {
        var templateService = new Mock<ITemplateService>();
        templateService
            .Setup(service => service.RenderAsync("template-1", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("Rendered template");

        var handler = new AIProfileCompletionContextBuilderHandler(templateService.Object);
        var profile = new AIProfile
        {
            ChatDeploymentName = "chat-deployment",
            UtilityDeploymentName = "utility-deployment",
        };

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "Base system message",
            Temperature = 0.4f,
            TopP = 0.8f,
            FrequencyPenalty = 0.2f,
            PresencePenalty = 0.1f,
            MaxTokens = 256,
            PastMessagesCount = 12,
            UseCaching = false,
        });
        profile.Put(new FunctionInvocationMetadata { Names = ["property-tool"] });
        profile.Put(new AgentInvocationMetadata { Names = ["property-agent"] });
        profile.Put(new PromptTemplateMetadata
        {
            Templates =
            [
                new PromptTemplateSelectionEntry
                {
                    TemplateId = "template-1",
                    Parameters = new Dictionary<string, object>
                    {
                        ["mode"] = "property",
                    },
                },
            ],
        });

        profile.WithSettings(new AIProfileMetadata
        {
            SystemMessage = "Settings system message",
            Temperature = 0.9f,
            UseCaching = true,
        });
        profile.WithSettings(new FunctionInvocationMetadata { Names = ["settings-tool"] });

        var completionContext = new AICompletionContext();
        var buildingContext = new AICompletionContextBuildingContext(profile, completionContext);

        await handler.BuildingAsync(buildingContext);

        Assert.Equal("chat-deployment", completionContext.ChatDeploymentName);
        Assert.Equal("utility-deployment", completionContext.UtilityDeploymentName);
        Assert.Equal("Rendered template\n\nBase system message", completionContext.SystemMessage);
        Assert.Equal(0.4f, completionContext.Temperature);
        Assert.Equal(0.8f, completionContext.TopP);
        Assert.Equal(0.2f, completionContext.FrequencyPenalty);
        Assert.Equal(0.1f, completionContext.PresencePenalty);
        Assert.Equal(256, completionContext.MaxTokens);
        Assert.Equal(12, completionContext.PastMessagesCount);
        Assert.False(completionContext.UseCaching);
        Assert.Equal(["property-tool"], completionContext.ToolNames);
        Assert.Equal(["property-agent"], completionContext.AgentNames);
    }
}
