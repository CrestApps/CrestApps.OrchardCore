using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.AI;

public sealed class AIProfileTemplateManagerTests
{
    private sealed class TestAIProfileTemplate : IAIProfileTemplate
    {
        public string Name => "Test";
        public LocalizedString DisplayName => new LocalizedString("Test", "Test Template");
        public LocalizedString Description => new LocalizedString("Description", "Test Description");
        public string ProfileSource => "OpenAI";

        public Task ApplyAsync(AIProfile profile)
        {
            profile.Type = AIProfileType.Chat;
            var metadata = profile.As<AIProfileMetadata>();
            metadata.SystemMessage = "Test system message";
            metadata.Temperature = 0.5f;
            profile.Put(metadata);
            return Task.CompletedTask;
        }
    }

    private sealed class UniversalTemplate : IAIProfileTemplate
    {
        public string Name => "Universal";
        public LocalizedString DisplayName => new LocalizedString("Universal", "Universal Template");
        public LocalizedString Description => new LocalizedString("Description", "Universal Description");
        public string ProfileSource => null; // Compatible with all sources

        public Task ApplyAsync(AIProfile profile)
        {
            profile.Type = AIProfileType.Utility;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void GetAllTemplates_ReturnsAllRegisteredTemplates()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate(),
            new UniversalTemplate()
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetAllTemplates();

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public void GetTemplatesForSource_ReturnsSourceSpecificAndUniversalTemplates()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate(), // OpenAI specific
            new UniversalTemplate() // Universal
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetTemplatesForSource("OpenAI");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Name == "Test");
        Assert.Contains(result, t => t.Name == "Universal");
    }

    [Fact]
    public void GetTemplatesForSource_ExcludesIncompatibleTemplates()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate(), // OpenAI specific
            new UniversalTemplate() // Universal
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetTemplatesForSource("Azure");

        // Assert
        Assert.Single(result);
        Assert.Contains(result, t => t.Name == "Universal");
        Assert.DoesNotContain(result, t => t.Name == "Test");
    }

    [Fact]
    public void GetTemplate_ReturnsTemplateByName()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate(),
            new UniversalTemplate()
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetTemplate("Test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public void GetTemplate_ReturnsNull_WhenTemplateNotFound()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate()
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetTemplate("NonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetTemplate_IsCaseInsensitive()
    {
        // Arrange
        var templates = new IAIProfileTemplate[]
        {
            new TestAIProfileTemplate()
        };
        var manager = new DefaultAIProfileTemplateManager(templates);

        // Act
        var result = manager.GetTemplate("test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task Template_ApplyAsync_ConfiguresProfile()
    {
        // Arrange
        var template = new TestAIProfileTemplate();
        var profile = new AIProfile
        {
            Source = "OpenAI",
            ItemId = "test-id"
        };

        // Act
        await template.ApplyAsync(profile);

        // Assert
        Assert.Equal(AIProfileType.Chat, profile.Type);
        var metadata = profile.As<AIProfileMetadata>();
        Assert.Equal("Test system message", metadata.SystemMessage);
        Assert.Equal(0.5f, metadata.Temperature);
    }
}
