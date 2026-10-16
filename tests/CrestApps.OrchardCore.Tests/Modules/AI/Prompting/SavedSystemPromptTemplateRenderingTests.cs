using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Providers;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Prompting;
using CrestApps.OrchardCore.AI.Prompting.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Prompting;

public sealed class SavedSystemPromptTemplateRenderingTests
{
    private const string _templateName = "saved-support-tone";

    [Fact]
    public async Task BuildAsync_WhenProfileSelectsSavedSystemPromptTemplate_ShouldRenderItIntoTheSystemMessage()
    {
        // Arrange
        var savedTemplates = new List<AIProfileTemplate>
        {
            CreateSystemPromptTemplate(_templateName, "Answer as {{ brand }} support."),
        };

        await using var serviceProvider = CreateServiceProvider(savedTemplates);

        // Act
        var systemMessage = await BuildSystemMessageAsync(serviceProvider, CreateProfile());

        // Assert
        Assert.Equal("Answer as Contoso support.\n\nBe brief.", systemMessage);
    }

    [Fact]
    public async Task BuildAsync_WhenSavedSystemPromptTemplateIsEdited_ShouldRenderTheEditedContent()
    {
        // Arrange
        var template = CreateSystemPromptTemplate(_templateName, "Answer as {{ brand }} support.");
        var savedTemplates = new List<AIProfileTemplate> { template };

        await using var serviceProvider = CreateServiceProvider(savedTemplates);

        var firstSystemMessage = await BuildSystemMessageAsync(serviceProvider, CreateProfile());

        template.Put(new SystemPromptTemplateMetadata
        {
            SystemMessage = "Answer as {{ brand }} support, in a friendly tone.",
        });

        // Act
        var editedSystemMessage = await BuildSystemMessageAsync(serviceProvider, CreateProfile());

        // Assert
        Assert.Equal("Answer as Contoso support.\n\nBe brief.", firstSystemMessage);
        Assert.Equal("Answer as Contoso support, in a friendly tone.\n\nBe brief.", editedSystemMessage);
    }

    [Fact]
    public async Task BuildAsync_WhenSavedSystemPromptTemplateIsAddedAfterFirstUse_ShouldRenderIt()
    {
        // Arrange
        var savedTemplates = new List<AIProfileTemplate>();

        await using var serviceProvider = CreateServiceProvider(savedTemplates);

        await BuildSystemMessageAsync(serviceProvider, new AIProfile());

        savedTemplates.Add(CreateSystemPromptTemplate(_templateName, "Answer as {{ brand }} support."));

        // Act
        var systemMessage = await BuildSystemMessageAsync(serviceProvider, CreateProfile());

        // Assert
        Assert.Equal("Answer as Contoso support.\n\nBe brief.", systemMessage);
    }

    [Fact]
    public async Task RenderAsync_WhenIdIsSavedSystemPromptTemplateName_ShouldRenderTheTemplate()
    {
        // Arrange
        var savedTemplates = new List<AIProfileTemplate>
        {
            CreateSystemPromptTemplate(_templateName, "Answer as {{ brand }} support."),
        };

        await using var serviceProvider = CreateServiceProvider(savedTemplates);
        await using var scope = serviceProvider.CreateAsyncScope();

        var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        // Act
        var rendered = await templateService.RenderAsync(
            _templateName,
            new Dictionary<string, object> { ["brand"] = "Contoso" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Answer as Contoso support.", rendered);
    }

    [Fact]
    public async Task RenderAsync_WhenSavedTemplateNameMatchesBuiltInTemplate_ShouldRenderTheBuiltInTemplate()
    {
        // Arrange
        var savedTemplates = new List<AIProfileTemplate>
        {
            CreateSystemPromptTemplate(_templateName, "Saved content."),
        };

        var builtInProvider = new Mock<ITemplateProvider>();
        builtInProvider
            .Setup(provider => provider.GetTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Template { Id = _templateName, Content = "Built-in content." }]);

        await using var serviceProvider = CreateServiceProvider(savedTemplates, builtInProvider.Object);
        await using var scope = serviceProvider.CreateAsyncScope();

        var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();

        // Act
        var rendered = await templateService.RenderAsync(_templateName, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Built-in content.", rendered);
    }

    private static async Task<string> BuildSystemMessageAsync(ServiceProvider serviceProvider, AIProfile profile)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextBuilder = scope.ServiceProvider.GetRequiredService<IAICompletionContextBuilder>();

        var context = await contextBuilder.BuildAsync(profile, cancellationToken: TestContext.Current.CancellationToken);

        return context.SystemMessage;
    }

    private static ServiceProvider CreateServiceProvider(List<AIProfileTemplate> savedTemplates, ITemplateProvider builtInProvider = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddMemoryCache();
        services.AddCoreAIServices();

        var shellFeaturesManager = new Mock<IShellFeaturesManager>();
        shellFeaturesManager
            .Setup(manager => manager.GetEnabledFeaturesAsync())
            .ReturnsAsync(Array.Empty<IFeatureInfo>());
        services.AddSingleton(shellFeaturesManager.Object);

        var templateManager = new Mock<IAIProfileTemplateManager>();
        templateManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IEnumerable<AIProfileTemplate>>(savedTemplates.ToList()));
        services.AddScoped(_ => templateManager.Object);

        new Startup().ConfigureServices(services);

        // Module discovery needs a running OrchardCore host; the saved templates are what is under test.
        services.Remove(services.Single(descriptor =>
            descriptor.ServiceType == typeof(ITemplateProvider) &&
            descriptor.ImplementationType == typeof(ModuleTemplateProvider)));

        if (builtInProvider is not null)
        {
            services.AddSingleton(builtInProvider);
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AIProfile CreateProfile()
    {
        var profile = new AIProfile();

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = "Be brief.",
        });

        profile.Put(new PromptTemplateMetadata
        {
            Templates =
            [
                new PromptTemplateSelectionEntry
                {
                    TemplateId = _templateName,
                    Parameters = new Dictionary<string, object>
                    {
                        ["brand"] = "Contoso",
                    },
                },
            ],
        });

        return profile;
    }

    private static AIProfileTemplate CreateSystemPromptTemplate(string name, string systemMessage)
    {
        var template = new AIProfileTemplate
        {
            ItemId = IdGenerator.GenerateId(),
            Name = name,
            DisplayText = name,
            Source = AITemplateSources.SystemPrompt,
            IsListable = true,
        };

        template.Put(new SystemPromptTemplateMetadata
        {
            SystemMessage = systemMessage,
        });

        return template;
    }
}
