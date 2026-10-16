using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Parsing;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Providers;
using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Personas;

public sealed class AIPersonaFeatureTests
{
    [Fact]
    public async Task PopulatePersonaPickerAsync_ShouldListEveryPersonaByTitleAndKeepAMissingLink()
    {
        // Arrange
        var personaManager = new Mock<IAIPersonaManager>();
        personaManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IEnumerable<AIPersona>>(
            [
                new AIPersona { Name = "tutor", DisplayText = "Patient tutor", VoiceName = "alloy" },
                new AIPersona { Name = "concierge", DisplayText = "Concierge", Description = "Polished." },
            ]));

        var profile = new AIProfile();
        profile.Put(new AIPersonaMetadata { PersonaId = "deleted-persona" });

        var model = new AIPersonaPickerViewModel();

        // Act
        await personaManager.Object.PopulatePersonaPickerAsync(model, profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("deleted-persona", model.PersonaId);
        Assert.Equal(["deleted-persona", "concierge", "tutor"], model.Personas.Select(option => option.Id));
        Assert.Equal("Concierge", model.Personas[1].Text);
        Assert.Equal("Polished.", model.Personas[1].Description);
        Assert.Equal("alloy", model.Personas[2].VoiceName);
    }

    [Fact]
    public void SetPersona_ShouldTrimTheNameAndClearItWhenEmpty()
    {
        // Arrange
        var profile = new AIProfile();

        // Act
        profile.SetPersona("  front-desk ");
        var linked = profile.GetOrCreate<AIPersonaMetadata>().PersonaId;

        profile.SetPersona(" ");
        var unlinked = profile.GetOrCreate<AIPersonaMetadata>().PersonaId;

        // Assert
        Assert.Equal("front-desk", linked);
        Assert.Null(unlinked);
    }

    [Fact]
    public async Task AIPersonaStep_WhenAPersonaHasTheSameName_ShouldUpdateIt()
    {
        // Arrange
        var saved = new AIPersona { ItemId = "saved-1", Name = "front-desk", Instructions = "Old." };

        var catalog = new Mock<INamedCatalog<AIPersona>>();
        catalog
            .Setup(c => c.FindByNameAsync("front-desk", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(saved));

        var personaManager = CreateValidatingManager();

        var step = new AIPersonaStep(personaManager.Object, catalog.Object, new PassThroughLocalizer<AIPersonaStep>());
        var context = CreateContext(new JsonObject
        {
            [nameof(AIPersona.Name)] = "front-desk",
            [nameof(AIPersona.Instructions)] = "New.",
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        personaManager.Verify(manager => manager.UpdateAsync(saved, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        personaManager.Verify(manager => manager.CreateAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIPersonaStep_WhenNoPersonaMatches_ShouldCreateOneWithTheGivenName()
    {
        // Arrange
        var created = new AIPersona { Name = "front-desk", Instructions = "Hello." };

        var personaManager = CreateValidatingManager();
        personaManager
            .Setup(manager => manager.NewAsync("front-desk", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(created));

        var step = new AIPersonaStep(personaManager.Object, Mock.Of<INamedCatalog<AIPersona>>(), new PassThroughLocalizer<AIPersonaStep>());
        var context = CreateContext(new JsonObject
        {
            [nameof(AIPersona.Name)] = " front-desk ",
            [nameof(AIPersona.Instructions)] = "Hello.",
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        personaManager.Verify(manager => manager.CreateAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIPersonaStep_WhenAPersonaHasNoName_ShouldReportAnErrorAndCreateNothing()
    {
        // Arrange
        var personaManager = CreateValidatingManager();

        var step = new AIPersonaStep(personaManager.Object, Mock.Of<INamedCatalog<AIPersona>>(), new PassThroughLocalizer<AIPersonaStep>());
        var context = CreateContext(new JsonObject
        {
            [nameof(AIPersona.Instructions)] = "Hello.",
        });

        // Act
        await step.ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);
        personaManager.Verify(manager => manager.CreateAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIPersonaDeploymentSource_ShouldExportOnlyTheSelectedSavedPersonas()
    {
        // Arrange
        var catalog = new Mock<INamedCatalog<AIPersona>>();
        catalog
            .Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIPersona>>(
            [
                new AIPersona { ItemId = "one", Name = "front-desk", Instructions = "Hello.", AssistantName = "Ava" },
                new AIPersona { ItemId = "two", Name = "tutor", Instructions = "Teach." },
            ]));

        var source = new AIPersonaDeploymentSource(catalog.Object);
        var step = new AIPersonaDeploymentStep { PersonaIds = ["one"] };
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(step, result);

        // Assert
        var exported = Assert.Single(result.Steps);
        Assert.Equal(AIPersonaStep.StepKey, exported["name"]!.GetValue<string>());

        var persona = Assert.Single(exported["personas"]!.AsArray());
        Assert.Equal("front-desk", persona![nameof(AIPersona.Name)]!.GetValue<string>());
        Assert.Equal("Hello.", persona[nameof(AIPersona.Instructions)]!.GetValue<string>());
        Assert.Equal("Ava", persona[nameof(AIPersona.AssistantName)]!.GetValue<string>());
    }

    [Fact]
    public void ShippedPersonaFiles_ShouldEachHaveATitleDescriptionAndInstructions()
    {
        // Arrange
        var directory = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.AI", "Templates", "Personas");
        var parser = new DefaultMarkdownTemplateParser();

        // Act
        var personas = Directory.GetFiles(directory, "*.md")
            .Select(file => AIPersonaFileParser.Parse(Path.GetFileNameWithoutExtension(file), parser.Parse(File.ReadAllText(file))))
            .ToList();

        // Assert
        Assert.Equal(6, personas.Count);
        Assert.All(personas, persona =>
        {
            Assert.False(string.IsNullOrWhiteSpace(persona.DisplayText), persona.Name);
            Assert.False(string.IsNullOrWhiteSpace(persona.Description), persona.Name);
            Assert.False(string.IsNullOrWhiteSpace(persona.Instructions), persona.Name);
            Assert.Equal("en-US", persona.Language);
        });
    }

    [Fact]
    public void PersonasStartup_ShouldRegisterTheCatalogResolverModuleFilesAndPickers()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new PersonasStartup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAIPersonaManager));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAIPersonaResolver));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INamedCatalog<AIPersona>));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAIPersonaProvider) && descriptor.ImplementationType == typeof(ModuleAIPersonaProvider));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(AIPersonaDisplayDriver));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(AIProfilePersonaDisplayDriver));
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(AIProfileTemplatePersonaDisplayDriver));
    }

    private static Mock<IAIPersonaManager> CreateValidatingManager()
    {
        var personaManager = new Mock<IAIPersonaManager>();
        personaManager
            .Setup(manager => manager.ValidateAsync(It.IsAny<AIPersona>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        return personaManager;
    }

    private static RecipeExecutionContext CreateContext(JsonObject persona)
        => new()
        {
            Name = AIPersonaStep.StepKey,
            Step = new JsonObject
            {
                ["name"] = AIPersonaStep.StepKey,
                ["personas"] = new JsonArray { persona },
            },
        };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
