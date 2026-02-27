using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Providers;
using CrestApps.AI.Prompting.Rendering;
using CrestApps.AI.Prompting.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

public sealed class DefaultAITemplateServiceTests
{
    [Fact]
    public async Task ListAsync_ReturnsAllTemplatesFromProviders()
    {
        var provider1 = new InMemoryProvider(
        [
            new AITemplate { Id = "p1", Content = "Prompt one" },
        ]);
        var provider2 = new InMemoryProvider(
        [
            new AITemplate { Id = "p2", Content = "Prompt two" },
        ]);

        var service = CreateService([provider1, provider2]);

        var templates = await service.ListAsync();

        Assert.Equal(2, templates.Count);
        Assert.Contains(templates, t => t.Id == "p1");
        Assert.Contains(templates, t => t.Id == "p2");
    }

    [Fact]
    public async Task ListAsync_NoProviders_ReturnsEmpty()
    {
        var service = CreateService([]);

        var templates = await service.ListAsync();

        Assert.Empty(templates);
    }

    [Fact]
    public async Task GetAsync_ExistingId_ReturnsTemplate()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "test-prompt", Content = "Hello world" },
        ]);

        var service = CreateService([provider]);

        var template = await service.GetAsync("test-prompt");

        Assert.NotNull(template);
        Assert.Equal("Hello world", template.Content);
    }

    [Fact]
    public async Task GetAsync_CaseInsensitive_ReturnsTemplate()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "Test-Prompt", Content = "Hello" },
        ]);

        var service = CreateService([provider]);

        var template = await service.GetAsync("test-prompt");

        Assert.NotNull(template);
    }

    [Fact]
    public async Task GetAsync_NonExistentId_ReturnsNull()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "existing", Content = "Hello" },
        ]);

        var service = CreateService([provider]);

        var template = await service.GetAsync("non-existent");

        Assert.Null(template);
    }

    [Fact]
    public async Task RenderAsync_ExistingTemplate_ReturnsRenderedContent()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "greeting", Content = "Hello, {{ name }}!" },
        ]);

        var service = CreateService([provider]);

        var result = await service.RenderAsync("greeting", new Dictionary<string, object>
        {
            ["name"] = "World",
        });

        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public async Task RenderAsync_PlainTemplate_ReturnsPlainText()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "simple", Content = "You are an AI." },
        ]);

        var service = CreateService([provider]);

        var result = await service.RenderAsync("simple");

        Assert.Equal("You are an AI.", result);
    }

    [Fact]
    public async Task RenderAsync_NonExistentTemplate_ThrowsKeyNotFoundException()
    {
        var service = CreateService([]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RenderAsync("missing"));
    }

    [Fact]
    public async Task MergeAsync_MultipleTemplates_ConcatenatesRendered()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "a", Content = "Part A" },
            new AITemplate { Id = "b", Content = "Part B" },
        ]);

        var service = CreateService([provider]);

        var result = await service.MergeAsync(["a", "b"]);

        Assert.Equal("Part A\n\nPart B", result);
    }

    [Fact]
    public async Task MergeAsync_CustomSeparator_UsesIt()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "x", Content = "X" },
            new AITemplate { Id = "y", Content = "Y" },
        ]);

        var service = CreateService([provider]);

        var result = await service.MergeAsync(["x", "y"], separator: " | ");

        Assert.Equal("X | Y", result);
    }

    [Fact]
    public async Task MergeAsync_ThrowsOnNonExistentTemplate()
    {
        var provider = new InMemoryProvider(
        [
            new AITemplate { Id = "a", Content = "Part A" },
        ]);

        var service = CreateService([provider]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MergeAsync(["a", "missing"]));
    }

    [Fact]
    public async Task MergeAsync_AllMissing_ThrowsKeyNotFoundException()
    {
        var service = CreateService([]);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MergeAsync(["missing1", "missing2"]));
    }

    private static DefaultAITemplateService CreateService(IAITemplateProvider[] providers)
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var renderer = new FluidAITemplateEngine(
            sp,
            NullLogger<FluidAITemplateEngine>.Instance);

        return new DefaultAITemplateService(providers, renderer);
    }

    private sealed class InMemoryProvider : IAITemplateProvider
    {
        private readonly IReadOnlyList<AITemplate> _templates;

        public InMemoryProvider(AITemplate[] templates)
        {
            _templates = templates;
        }

        public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
        {
            return Task.FromResult(_templates);
        }
    }
}
