using CrestApps.AI;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Clients;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.Templates.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.AI;

public sealed class DataExtractionServiceTests
{
    [Fact]
    public async Task ProcessAsync_WhenExtractionIsDisabled_ShouldReturnNull()
    {
        var service = CreateService();
        var profile = CreateProfile(settings =>
        {
            settings.EnableDataExtraction = false;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry { Name = "email" },
            ];
        });

        var result = await service.ProcessAsync(profile, new AIChatSession(), CreatePrompts("hello"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_WhenPromptCountDoesNotMatchInterval_ShouldReturnNull()
    {
        var service = CreateService();
        var profile = CreateProfile(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.ExtractionCheckInterval = 2;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry { Name = "email" },
            ];
        });

        var result = await service.ProcessAsync(profile, new AIChatSession(), CreatePrompts("first"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_WhenOnlyNonUpdatableFieldsAlreadyExist_ShouldReturnNull()
    {
        var service = CreateService();
        var profile = CreateProfile(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.ExtractionCheckInterval = 1;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry
                {
                    Name = "email",
                    AllowMultipleValues = false,
                    IsUpdatable = false,
                },
            ];
        });
        var session = new AIChatSession
        {
            ExtractedData =
            {
                ["email"] = new ExtractedFieldState
                {
                    Values = ["user@example.com"],
                },
            },
        };

        var result = await service.ProcessAsync(profile, session, CreatePrompts("hello"), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessAsync_UsesTemplateForExtractionPrompt()
    {
        var clientFactory = new Mock<IAIClientFactory>();
        var templateService = new Mock<ITemplateService>();
        var deploymentManager = new Mock<IAIDeploymentManager>();
        var chatClient = new Mock<IChatClient>();

        var profile = CreateProfile(settings =>
        {
            settings.EnableDataExtraction = true;
            settings.ExtractionCheckInterval = 1;
            settings.DataExtractionEntries =
            [
                new DataExtractionEntry
                {
                    Name = "email",
                    Description = "The user's email address.",
                    IsUpdatable = true,
                },
            ];
        });
        profile.UtilityDeploymentName = "utility";

        deploymentManager
            .Setup(manager => manager.ResolveOrDefaultAsync(AIDeploymentType.Utility, "utility", null, null))
            .ReturnsAsync(new AIDeployment
            {
                ClientName = "OpenAI",
                ConnectionName = "Default",
                ModelName = "gpt-4.1",
            });

        deploymentManager
            .Setup(manager => manager.ResolveOrDefaultAsync(AIDeploymentType.Chat, null, null, null))
            .ReturnsAsync(new AIDeployment
            {
                ClientName = "OpenAI",
                ConnectionName = "Default",
                ModelName = "gpt-4.1",
            });

        clientFactory
            .Setup(factory => factory.CreateChatClientAsync("OpenAI", "Default", "gpt-4.1"))
            .ReturnsAsync(chatClient.Object);

        templateService
            .Setup(service => service.RenderAsync(AITemplateIds.DataExtraction, It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync("system prompt");

        IDictionary<string, object> promptArguments = null;
        templateService
            .Setup(service => service.RenderAsync(AITemplateIds.DataExtractionPrompt, It.IsAny<IDictionary<string, object>>()))
            .Callback<string, IDictionary<string, object>>((_, arguments) => promptArguments = arguments)
            .ReturnsAsync("rendered prompt");

        chatClient
            .Setup(client => client.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"fields\":[],\"sessionEnded\":false}")));

        var service = new DataExtractionService(
            clientFactory.Object,
            templateService.Object,
            TimeProvider.System,
            NullLogger<DataExtractionService>.Instance,
            deploymentManager.Object);

        await service.ProcessAsync(
            profile,
            new AIChatSession(),
            [
                new AIChatSessionPrompt { Role = ChatRole.Assistant, Content = "What is your email?" },
                new AIChatSessionPrompt { Role = ChatRole.User, Content = "My email is test@example.com" },
            ],
            TestContext.Current.CancellationToken);

        templateService.Verify(service => service.RenderAsync(AITemplateIds.DataExtractionPrompt, It.IsAny<IDictionary<string, object>>()), Times.Once);
        Assert.NotNull(promptArguments);
        Assert.Equal("My email is test@example.com", promptArguments["lastUserMessage"]);
        Assert.Equal("What is your email?", promptArguments["lastAssistantMessage"]);
    }

    private static DataExtractionService CreateService()
    {
        var clientFactory = new Mock<IAIClientFactory>();
        var templateService = new Mock<ITemplateService>();
        var deploymentManager = new Mock<IAIDeploymentManager>();

        return new DataExtractionService(
            clientFactory.Object,
            templateService.Object,
            TimeProvider.System,
            NullLogger<DataExtractionService>.Instance,
            deploymentManager.Object);
    }

    private static AIProfile CreateProfile(Action<AIProfileDataExtractionSettings> configure)
    {
        var profile = new AIProfile();
        profile.AlterSettings(configure);
        return profile;
    }

    private static AIChatSessionPrompt[] CreatePrompts(params string[] userMessages) =>
        userMessages.Select(message => new AIChatSessionPrompt
        {
            Role = ChatRole.User,
            Content = message,
        }).ToArray();
}
