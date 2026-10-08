using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class ProfileScenarioCatalogTests
{
    private const string _chatFeatureId = "CrestApps.OrchardCore.AI.Chat";
    private const string _documentsFeatureId = "CrestApps.OrchardCore.AI.Documents.Profiles";

    [Fact]
    public async Task GetPickerScenariosAsync_ShouldLeaveOutTemplatePromptsAndUnlistedTemplates()
    {
        // Arrange
        var catalog = CreateCatalog(
        [
            CreateTemplate("chat", AIProfileType.Chat),
            CreateTemplate("prompt", AIProfileType.TemplatePrompt),
            CreateTemplate("hidden", AIProfileType.Utility, isListable: false),
        ]);

        // Act
        var scenarios = await catalog.GetPickerScenariosAsync();

        // Assert
        var scenario = Assert.Single(scenarios);
        Assert.Equal("chat", scenario.TemplateId);
    }

    [Fact]
    public async Task GetPickerScenariosAsync_ShouldListFeaturedScenariosFirstInTheirWrittenOrder()
    {
        // Arrange
        var catalog = CreateCatalog(
        [
            CreateTemplate("agent", AIProfileType.Agent, category: "Analysis"),
            CreateTemplate("summarizer", AIProfileType.Utility, category: "Background", featured: true, order: 11),
            CreateTemplate("intake", AIProfileType.Chat, category: "Talk to people", featured: true, order: 3),
            CreateTemplate("website", AIProfileType.Chat, category: "Talk to people", featured: true, order: 1),
            CreateTemplate("uncategorized", AIProfileType.Agent),
        ]);

        // Act
        var scenarios = await catalog.GetPickerScenariosAsync();

        // Assert
        Assert.Equal(["website", "intake", "summarizer", "agent", "uncategorized"], scenarios.Select(scenario => scenario.TemplateId));
        Assert.Equal([true, true, true, false, false], scenarios.Select(scenario => scenario.IsFeatured));
    }

    [Fact]
    public async Task GetPickerScenariosAsync_ShouldNameTheFeaturesAScenarioStillNeeds()
    {
        // Arrange
        var catalog = CreateCatalog(
            [CreateTemplate("docs", AIProfileType.Chat, featured: true, requiresFeatures: [_chatFeatureId, _documentsFeatureId])],
            enabledFeatureIds: [_chatFeatureId]);

        // Act
        var scenario = Assert.Single(await catalog.GetPickerScenariosAsync());

        // Assert
        Assert.False(scenario.IsAvailable);
        Assert.Equal(["AI Documents for Profiles"], scenario.MissingFeatureNames);
    }

    [Fact]
    public async Task GetScenarioAsync_WhenEveryRequiredFeatureIsEnabled_ShouldBeAvailable()
    {
        // Arrange
        var template = CreateTemplate("website", AIProfileType.Chat, featured: true, requiresFeatures: [_chatFeatureId]);
        var catalog = CreateCatalog([template], enabledFeatureIds: [_chatFeatureId]);

        // Act
        var scenario = await catalog.GetScenarioAsync(template);

        // Assert
        Assert.True(scenario.IsAvailable);
        Assert.Equal("fa-solid fa-star", scenario.Icon);
        Assert.Equal(AIProfileType.Chat, scenario.ProfileType);
    }

    private static AIProfileTemplate CreateTemplate(
        string id,
        AIProfileType profileType,
        string category = null,
        bool isListable = true,
        bool featured = false,
        int order = 0,
        string[] requiresFeatures = null)
    {
        var template = new AIProfileTemplate
        {
            ItemId = id,
            Name = id,
            DisplayText = id,
            Source = AITemplateSources.Profile,
            Category = category,
            IsListable = isListable,
        };

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = profileType,
        });

        if (featured || requiresFeatures is not null)
        {
            template.Put(new ProfileScenarioMetadata
            {
                Featured = featured,
                Icon = "fa-solid fa-star",
                Order = order,
                RequiresFeatures = requiresFeatures ?? [],
            });
        }

        return template;
    }

    private static ProfileScenarioCatalog CreateCatalog(IEnumerable<AIProfileTemplate> templates, string[] enabledFeatureIds = null)
    {
        var templateManager = new Mock<IAIProfileTemplateManager>();
        templateManager
            .Setup(manager => manager.GetAsync(AITemplateSources.Profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var availableFeatures = new[]
        {
            CreateFeature(_chatFeatureId, "AI Chat"),
            CreateFeature(_documentsFeatureId, "AI Documents for Profiles"),
        };

        var shellFeaturesManager = new Mock<IShellFeaturesManager>();
        shellFeaturesManager
            .Setup(manager => manager.GetAvailableFeaturesAsync())
            .ReturnsAsync(availableFeatures);
        shellFeaturesManager
            .Setup(manager => manager.GetEnabledFeaturesAsync())
            .ReturnsAsync(availableFeatures.Where(feature => (enabledFeatureIds ?? []).Contains(feature.Id)).ToArray());

        return new ProfileScenarioCatalog(templateManager.Object, shellFeaturesManager.Object);
    }

    private static IFeatureInfo CreateFeature(string id, string name)
    {
        var feature = new Mock<IFeatureInfo>();
        feature.SetupGet(f => f.Id).Returns(id);
        feature.SetupGet(f => f.Name).Returns(name);

        return feature.Object;
    }
}
