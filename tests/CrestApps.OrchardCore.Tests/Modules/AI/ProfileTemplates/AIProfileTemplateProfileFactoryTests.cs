using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class AIProfileTemplateProfileFactoryTests
{
    private static readonly DefaultAIOptions _defaults = new()
    {
        Temperature = 0.9f,
        TopP = 0.8f,
        FrequencyPenalty = 0.1f,
        PresencePenalty = 0.2f,
        MaxOutputTokens = 512,
        PastMessagesCount = 12,
    };

    [Fact]
    public async Task NewFromTemplateAsync_ShouldApplyTheTemplateAndKeepItsValues()
    {
        // Arrange
        var profileManager = CreateProfileManager("profile-1");
        var factory = CreateFactory(profileManager.Object, []);

        var template = new AIProfileTemplate
        {
            ItemId = "reviewer-agent",
            Name = "reviewer-agent",
            DisplayText = "Reviewer",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Agent,
            SystemMessage = "Review the work you are given.",
            Description = "Reviews work for accuracy.",
            Temperature = 0.2f,
        });

        // Act
        var profile = await factory.NewFromTemplateAsync(template);

        // Assert
        Assert.Equal("profile-1", profile.ItemId);
        Assert.Equal(AIProfileType.Agent, profile.Type);
        Assert.Equal("Reviews work for accuracy.", profile.Description);

        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal("Review the work you are given.", metadata.SystemMessage);
        Assert.Equal(0.2f, metadata.Temperature);
    }

    [Fact]
    public async Task NewFromTemplateAsync_ForAChatTemplate_ShouldFillMissingParametersFromTheDefaults()
    {
        // Arrange
        var profileManager = CreateProfileManager("profile-1");
        var factory = CreateFactory(profileManager.Object, []);

        var template = new AIProfileTemplate
        {
            Name = "website-assistant",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Chat,
            Temperature = 0.5f,
        });

        // Act
        var profile = await factory.NewFromTemplateAsync(template);

        // Assert
        var metadata = profile.GetOrCreate<AIProfileMetadata>();
        Assert.Equal(0.5f, metadata.Temperature);
        Assert.Equal(_defaults.TopP, metadata.TopP);
        Assert.Equal(_defaults.FrequencyPenalty, metadata.FrequencyPenalty);
        Assert.Equal(_defaults.PresencePenalty, metadata.PresencePenalty);
        Assert.Equal(_defaults.MaxOutputTokens, metadata.MaxTokens);

        // A chat profile cannot be saved in the editor without a past messages count.
        Assert.Equal(_defaults.PastMessagesCount, metadata.PastMessagesCount);
    }

    [Fact]
    public async Task NewFromTemplateAsync_ForATemplatePromptTemplate_ShouldCarryThePromptAndLeaveThePastMessagesCountUnset()
    {
        // Arrange
        var profileManager = CreateProfileManager("profile-1");
        var factory = CreateFactory(profileManager.Object, []);

        var template = new AIProfileTemplate
        {
            Name = "content-generator",
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.TemplatePrompt,
            PromptTemplate = "Write about {{ Session.Title }}",
            PromptSubject = "Draft",
        });

        // Act
        var profile = await factory.NewFromTemplateAsync(template);

        // Assert
        Assert.Equal(AIProfileType.TemplatePrompt, profile.Type);
        Assert.Equal("Write about {{ Session.Title }}", profile.PromptTemplate);
        Assert.Equal("Draft", profile.PromptSubject);
        Assert.Null(profile.GetOrCreate<AIProfileMetadata>().PastMessagesCount);
    }

    [Fact]
    public async Task NewFromTemplateAsync_ShouldNeitherRunHandlersNorPersist()
    {
        // Arrange
        var profileManager = CreateProfileManager("profile-1");
        var handler = new Mock<IAIProfileTemplateApplicationHandler>();
        var factory = CreateFactory(profileManager.Object, [handler.Object]);

        // Act
        await factory.NewFromTemplateAsync(new AIProfileTemplate { Name = "blank" });

        // Assert
        handler.Verify(h => h.AppliedAsync(It.IsAny<AIProfileTemplateAppliedContext>()), Times.Never);
        profileManager.Verify(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldRunEachHandlerOnceBeforePersisting()
    {
        // Arrange
        var calls = new List<string>();
        var profileManager = CreateProfileManager("profile-1");
        profileManager
            .Setup(m => m.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("create"))
            .Returns(ValueTask.CompletedTask);

        AIProfileTemplateAppliedContext appliedContext = null;
        var handler = new Mock<IAIProfileTemplateApplicationHandler>();
        handler
            .Setup(h => h.AppliedAsync(It.IsAny<AIProfileTemplateAppliedContext>()))
            .Callback<AIProfileTemplateAppliedContext>(context =>
            {
                appliedContext = context;
                calls.Add("handler");
            })
            .Returns(Task.CompletedTask);

        var factory = CreateFactory(profileManager.Object, [handler.Object]);
        var template = new AIProfileTemplate { Name = "website-assistant" };
        var profile = await factory.NewFromTemplateAsync(template);

        // Act
        await factory.CreateAsync(profile, template);

        // Assert
        handler.Verify(h => h.AppliedAsync(It.IsAny<AIProfileTemplateAppliedContext>()), Times.Once);
        profileManager.Verify(m => m.CreateAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(["handler", "create"], calls);
        Assert.Same(profile, appliedContext.Profile);
        Assert.Same(template, appliedContext.Template);
    }

    private static Mock<IAIProfileManager> CreateProfileManager(string itemId)
    {
        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AIProfile { ItemId = itemId });

        return profileManager;
    }

    private static AIProfileTemplateProfileFactory CreateFactory(
        IAIProfileManager profileManager,
        IEnumerable<IAIProfileTemplateApplicationHandler> handlers)
        => new(
            profileManager,
            handlers,
            Options.Create(_defaults),
            NullLogger<AIProfileTemplateProfileFactory>.Instance);
}
