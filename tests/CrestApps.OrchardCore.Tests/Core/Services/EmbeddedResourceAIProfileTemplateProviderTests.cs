using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Services;
using CrestApps.Templates.Parsing;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class EmbeddedResourceAIProfileTemplateProviderTests
{
    [Fact]
    public async Task GetTemplatesAsync_DiscoversAllEmbeddedProfileTemplates()
    {
        var provider = new EmbeddedResourceAIProfileTemplateProvider(
            typeof(CrestApps.AI.ServiceCollectionExtensions).Assembly,
            [new DefaultMarkdownTemplateParser()]);

        var templates = await provider.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Contains(templates, template => template.ItemId == "chat-session-summarizer");
        Assert.All(templates, template => Assert.Equal(AITemplateSources.Profile, template.Source));
    }

    [Fact]
    public async Task GetTemplatesAsync_MapsFrontMatterToProfileMetadata()
    {
        var provider = new EmbeddedResourceAIProfileTemplateProvider(
            typeof(CrestApps.AI.ServiceCollectionExtensions).Assembly,
            [new DefaultMarkdownTemplateParser()]);

        var templates = await provider.GetTemplatesAsync();
        var template = templates.Single(t => t.ItemId == "chat-session-summarizer");
        var metadata = template.As<ProfileTemplateMetadata>();

        Assert.Equal(AIProfileType.TemplatePrompt, metadata.ProfileType);
        Assert.Equal(0.3f, metadata.Temperature);
        Assert.Equal("Summarizes the current chat session into a concise summary with key points and action items.", template.Description);
    }
}
