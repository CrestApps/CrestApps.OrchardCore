using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class ProfileScenarioMetadataReaderTests
{
    [Fact]
    public void Apply_WhenAllKeysArePresent_ShouldStoreEveryValue()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["Featured"] = "true",
            ["Icon"] = "fa-solid fa-comments",
            ["Order"] = "7",
            ["RequiresFeatures"] = "Feature.One,Feature.Two",
        });

        // Act
        ProfileScenarioMetadataReader.Apply(template, metadata);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.True(scenario.Featured);
        Assert.Equal("fa-solid fa-comments", scenario.Icon);
        Assert.Equal(7, scenario.Order);
        Assert.Equal(["Feature.One", "Feature.Two"], scenario.RequiresFeatures);
    }

    [Fact]
    public void Apply_WhenNoScenarioKeyIsPresent_ShouldNotStoreMetadata()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["ProfileType"] = "Chat",
            ["Temperature"] = "0.3",
        });

        // Act
        ProfileScenarioMetadataReader.Apply(template, metadata);

        // Assert
        Assert.False(template.Has<ProfileScenarioMetadata>());
    }

    [Fact]
    public void Apply_WhenOrderIsMalformed_ShouldIgnoreTheOrder()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["Featured"] = "true",
            ["Order"] = "first",
        });

        // Act
        ProfileScenarioMetadataReader.Apply(template, metadata);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.True(scenario.Featured);
        Assert.Equal(0, scenario.Order);
    }

    [Fact]
    public void Apply_WhenFeaturedIsMalformed_ShouldIgnoreIt()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["Featured"] = "sometimes",
        });

        // Act
        ProfileScenarioMetadataReader.Apply(template, metadata);

        // Assert
        Assert.False(template.Has<ProfileScenarioMetadata>());
    }

    [Fact]
    public void Apply_WhenRequiresFeaturesHasSpacesAndEmptyEntries_ShouldSplitAndTrim()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["RequiresFeatures"] = " CrestApps.OrchardCore.AI.Chat ,  , CrestApps.OrchardCore.AI.Documents.Profiles ",
        });

        // Act
        ProfileScenarioMetadataReader.Apply(template, metadata);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.Equal(["CrestApps.OrchardCore.AI.Chat", "CrestApps.OrchardCore.AI.Documents.Profiles"], scenario.RequiresFeatures);
        Assert.False(scenario.Featured);
    }

    [Fact]
    public void Apply_WhenReadFromMarkdownFrontMatter_ShouldFindTheKeysTheParserLeavesUnknown()
    {
        // Arrange
        const string content = """
            ---
            Title: Website assistant
            Category: Talk to people
            Featured: true
            Icon: fa-solid fa-comments
            Order: 2
            RequiresFeatures: CrestApps.OrchardCore.AI.Chat
            ProfileType: Chat
            ---

            You are a helpful assistant.
            """;

        var parseResult = new DefaultMarkdownTemplateParser().Parse(content);
        var template = AIProfileTemplateParser.Parse("website-assistant", parseResult);

        // Act
        ProfileScenarioMetadataReader.Apply(template, parseResult.Metadata);

        // Assert
        Assert.True(template.TryGet<ProfileScenarioMetadata>(out var scenario));
        Assert.True(scenario.Featured);
        Assert.Equal(2, scenario.Order);
        Assert.Equal(["CrestApps.OrchardCore.AI.Chat"], scenario.RequiresFeatures);
        Assert.Equal(AIProfileType.Chat, template.GetOrCreate<ProfileTemplateMetadata>().ProfileType);
    }

    private static TemplateMetadata CreateMetadata(Dictionary<string, string> additionalProperties)
        => new()
        {
            AdditionalProperties = additionalProperties,
        };
}
