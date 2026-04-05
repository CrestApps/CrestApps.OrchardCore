using CrestApps.AI.Clients;
using CrestApps.AI.Chat.Services;
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

        var result = await service.ProcessAsync(profile, new AIChatSession(), CreatePrompts("hello"));

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

        var result = await service.ProcessAsync(profile, new AIChatSession(), CreatePrompts("first"));

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

        var result = await service.ProcessAsync(profile, session, CreatePrompts("hello"));

        Assert.Null(result);
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
