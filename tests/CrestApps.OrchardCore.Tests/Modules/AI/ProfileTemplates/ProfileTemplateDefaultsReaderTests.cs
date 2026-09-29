using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class ProfileTemplateDefaultsReaderTests
{
    [Fact]
    public void Apply_WhenInitialPromptIsPresent_ShouldStoreItTrimmed()
    {
        // Arrange
        var template = new AIProfileTemplate();
        var metadata = CreateMetadata(new()
        {
            ["InitialPrompt"] = "  Hi {{ Contact.DisplayText }}, thanks for reaching out.  ",
        });

        // Act
        ProfileTemplateDefaultsReader.Apply(template, metadata);

        // Assert
        Assert.True(template.TryGet<ProfileTemplateDefaultsMetadata>(out var defaults));
        Assert.Equal("Hi {{ Contact.DisplayText }}, thanks for reaching out.", defaults.InitialPrompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_WhenInitialPromptIsMissingOrBlank_ShouldNotStoreMetadata(string initialPrompt)
    {
        // Arrange
        var template = new AIProfileTemplate();
        var properties = new Dictionary<string, string>
        {
            ["ProfileType"] = "Chat",
        };

        if (initialPrompt is not null)
        {
            properties["InitialPrompt"] = initialPrompt;
        }

        // Act
        ProfileTemplateDefaultsReader.Apply(template, CreateMetadata(properties));

        // Assert
        Assert.False(template.Has<ProfileTemplateDefaultsMetadata>());
    }

    [Fact]
    public void Apply_WhenMetadataIsNull_ShouldLeaveTheTemplateUnchanged()
    {
        // Arrange
        var template = new AIProfileTemplate();

        // Act
        ProfileTemplateDefaultsReader.Apply(template, null);

        // Assert
        Assert.Empty(template.Properties);
    }

    private static TemplateMetadata CreateMetadata(Dictionary<string, string> additionalProperties)
        => new()
        {
            AdditionalProperties = additionalProperties,
        };
}
