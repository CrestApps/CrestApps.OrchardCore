using CrestApps.AI.Prompting;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.AI.Prompting.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

public sealed class OptionsAITemplateProviderTests
{
    [Fact]
    public async Task GetTemplatesAsync_ReturnsRegisteredTemplates()
    {
        var options = new AITemplateOptions();
        options.Templates.Add(new AITemplate { Id = "code-template", Content = "Registered via code." });
        options.Templates.Add(new AITemplate { Id = "another", Content = "Another one." });

        var provider = new OptionsAITemplateProvider(Options.Create(options));

        var result = await provider.GetTemplatesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == "code-template");
        Assert.Contains(result, t => t.Id == "another");
    }

    [Fact]
    public async Task GetTemplatesAsync_EmptyOptions_ReturnsEmpty()
    {
        var options = new AITemplateOptions();
        var provider = new OptionsAITemplateProvider(Options.Create(options));

        var result = await provider.GetTemplatesAsync();

        Assert.Empty(result);
    }
}

public sealed class FileSystemAITemplateProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FileSystemAITemplateProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CrestAppsPromptTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetTemplatesAsync_DiscoversMdFiles()
    {
        var promptsDir = Path.Combine(_tempDir, "AITemplates", "Prompts");
        Directory.CreateDirectory(promptsDir);

        File.WriteAllText(Path.Combine(promptsDir, "test-prompt.md"), """
            ---
            Title: Test Prompt
            Description: A test prompt
            ---
            You are a test assistant.
            """);

        var options = new AITemplateOptions();
        options.DiscoveryPaths.Add(_tempDir);

        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };
        var provider = new FileSystemAITemplateProvider(
            Options.Create(options),
            parsers,
            NullLogger<FileSystemAITemplateProvider>.Instance);

        var templates = await provider.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("test-prompt", templates[0].Id);
        Assert.Equal("Test Prompt", templates[0].Metadata.Title);
        Assert.Contains("You are a test assistant.", templates[0].Content);
    }

    [Fact]
    public async Task GetTemplatesAsync_DiscoverFeatureSubdirectories()
    {
        var promptsDir = Path.Combine(_tempDir, "AITemplates", "Prompts");
        var featureDir = Path.Combine(promptsDir, "MyModule.Feature");
        Directory.CreateDirectory(featureDir);

        File.WriteAllText(Path.Combine(featureDir, "feature-prompt.md"), """
            ---
            Title: Feature Prompt
            ---
            Feature-specific content.
            """);

        var options = new AITemplateOptions();
        options.DiscoveryPaths.Add(_tempDir);

        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };
        var provider = new FileSystemAITemplateProvider(
            Options.Create(options),
            parsers,
            NullLogger<FileSystemAITemplateProvider>.Instance);

        var templates = await provider.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("feature-prompt", templates[0].Id);
        Assert.Equal("MyModule.Feature", templates[0].FeatureId);
    }

    [Fact]
    public async Task GetTemplatesAsync_NoPromptsDirectory_ReturnsEmpty()
    {
        var options = new AITemplateOptions();
        options.DiscoveryPaths.Add(_tempDir);

        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };
        var provider = new FileSystemAITemplateProvider(
            Options.Create(options),
            parsers,
            NullLogger<FileSystemAITemplateProvider>.Instance);

        var templates = await provider.GetTemplatesAsync();

        Assert.Empty(templates);
    }

    [Fact]
    public async Task GetTemplatesAsync_UsesFilenameAsTitleWhenNotInFrontMatter()
    {
        var promptsDir = Path.Combine(_tempDir, "AITemplates", "Prompts");
        Directory.CreateDirectory(promptsDir);

        File.WriteAllText(Path.Combine(promptsDir, "my-cool-prompt.md"), "Just body, no front matter.");

        var options = new AITemplateOptions();
        options.DiscoveryPaths.Add(_tempDir);

        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };
        var provider = new FileSystemAITemplateProvider(
            Options.Create(options),
            parsers,
            NullLogger<FileSystemAITemplateProvider>.Instance);

        var templates = await provider.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("my cool prompt", templates[0].Metadata.Title);
    }

    [Fact]
    public async Task GetTemplatesAsync_IgnoresNonMdFiles()
    {
        var promptsDir = Path.Combine(_tempDir, "AITemplates", "Prompts");
        Directory.CreateDirectory(promptsDir);

        File.WriteAllText(Path.Combine(promptsDir, "valid.md"), "Valid prompt.");
        File.WriteAllText(Path.Combine(promptsDir, "readme.txt"), "Not a prompt.");
        File.WriteAllText(Path.Combine(promptsDir, "data.json"), "{}");

        var options = new AITemplateOptions();
        options.DiscoveryPaths.Add(_tempDir);

        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };
        var provider = new FileSystemAITemplateProvider(
            Options.Create(options),
            parsers,
            NullLogger<FileSystemAITemplateProvider>.Instance);

        var templates = await provider.GetTemplatesAsync();

        Assert.Single(templates);
        Assert.Equal("valid", templates[0].Id);
    }
}

public sealed class EmbeddedResourceAITemplateProviderTests
{
    [Fact]
    public async Task GetTemplatesAsync_DiscoversEmbeddedResources()
    {
        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };

        // Use the test assembly which has embedded AI/Prompts/*.md files.
        var assembly = typeof(EmbeddedResourceAITemplateProviderTests).Assembly;
        var provider = new EmbeddedResourceAITemplateProvider(assembly, parsers);

        var templates = await provider.GetTemplatesAsync();

        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.Id == "test-template");
    }

    [Fact]
    public async Task GetTemplatesAsync_ParsesFrontMatter()
    {
        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };

        var assembly = typeof(EmbeddedResourceAITemplateProviderTests).Assembly;
        var provider = new EmbeddedResourceAITemplateProvider(assembly, parsers);

        var templates = await provider.GetTemplatesAsync();

        var testTemplate = templates.FirstOrDefault(t => t.Id == "test-template");
        Assert.NotNull(testTemplate);
        Assert.Equal("Test Template", testTemplate.Metadata.Title);
        Assert.True(testTemplate.Metadata.IsListable);
        Assert.Equal("Testing", testTemplate.Metadata.Category);
        Assert.NotEmpty(testTemplate.Content);
    }

    [Fact]
    public async Task GetTemplatesAsync_SetsSourceFromAssemblyName()
    {
        var options = new AITemplateOptions();
        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };

        var assembly = typeof(EmbeddedResourceAITemplateProviderTests).Assembly;
        var provider = new EmbeddedResourceAITemplateProvider(assembly, parsers);

        var templates = await provider.GetTemplatesAsync();

        Assert.All(templates, t => Assert.Equal(assembly.GetName().Name, t.Source));
    }

    [Fact]
    public async Task GetTemplatesAsync_CustomSourceAndFeatureId()
    {
        var options = new AITemplateOptions();
        var parsers = new IAITemplateParser[] { new DefaultMarkdownAITemplateParser() };

        var assembly = typeof(EmbeddedResourceAITemplateProviderTests).Assembly;
        var provider = new EmbeddedResourceAITemplateProvider(assembly, parsers, source: "MySource", featureId: "MyFeature");

        var templates = await provider.GetTemplatesAsync();

        Assert.All(templates, t =>
        {
            Assert.Equal("MySource", t.Source);
            Assert.Equal("MyFeature", t.FeatureId);
        });
    }
}
