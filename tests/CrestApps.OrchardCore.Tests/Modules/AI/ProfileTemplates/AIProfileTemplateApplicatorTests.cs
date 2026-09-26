using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.ProfileTemplates;

public sealed class AIProfileTemplateApplicatorTests
{
    [Fact]
    public void Apply_WhenTemplateHasScenarioMetadata_ShouldNotCopyItOntoTheProfile()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            Name = "website-assistant",
            DisplayText = "Website assistant",
        };

        template.Put(new ProfileScenarioMetadata
        {
            Featured = true,
            Icon = "fa-solid fa-comments",
            Order = 1,
            RequiresFeatures = ["CrestApps.OrchardCore.AI.Chat"],
        });

        template.Put(new ProfileTemplateMetadata
        {
            ProfileType = AIProfileType.Chat,
        });

        var profile = new AIProfile();

        // Act
        AIProfileTemplateApplicator.Apply(profile, template);

        // Assert
        Assert.False(profile.Has<ProfileScenarioMetadata>());
        Assert.False(profile.Settings.ContainsKey(nameof(ProfileScenarioMetadata)));
        Assert.Equal(AIProfileType.Chat, profile.Type);
    }
}
