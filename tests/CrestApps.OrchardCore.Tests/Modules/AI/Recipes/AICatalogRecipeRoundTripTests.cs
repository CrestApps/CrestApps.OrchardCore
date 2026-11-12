using CrestApps.Core;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.DataSources.Deployments;
using CrestApps.OrchardCore.AI.DataSources.Recipes;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Recipes;

/// <summary>
/// Pins the JSON the AI recipe steps read and the AI deployment sources write, now that both run on the shared catalog
/// recipe and deployment bases: the recipe samples published in the documentation still import, and what a deployment
/// source exports imports back through the matching step.
/// </summary>
public sealed class AICatalogRecipeRoundTripTests
{
    // The provider recipe published in docs/ai/providers/openai.md.
    private const string OpenAIProviderRecipe = """
        {
          "steps": [
            {
              "name": "AIProviderConnections",
              "Connections": [
                {
                  "Source": "OpenAI",
                  "Name": "deepseek",
                  "DisplayText": "DeepSeek",
                  "Properties": {
                    "OpenAIConnectionMetadata": {
                      "Endpoint": "https://api.deepseek.com/v1",
                      "ApiKey": "your-deepseek-api-key"
                    }
                  }
                }
              ]
            },
            {
              "name": "AIDeployment",
              "Deployments": [
                {
                  "Name": "deepseek-chat",
                  "ModelName": "deepseek-chat",
                  "ClientName": "OpenAI",
                  "ConnectionName": "deepseek",
                  "Properties": {
                    "AIDeploymentMetadata": {
                      "Features": [ "textGeneration", "toolCalling", "streaming" ]
                    }
                  }
                },
                {
                  "Name": "deepseek-reasoner",
                  "ModelName": "deepseek-reasoner",
                  "ClientName": "OpenAI",
                  "ConnectionName": "deepseek",
                  "Properties": {
                    "AIDeploymentMetadata": {
                      "Features": [ "textGeneration", "reasoning", "streaming" ]
                    }
                  }
                }
              ]
            }
          ]
        }
        """;

    // The tool instance recipe published in docs/ai/tool-instances.md, with the step array in lower case as written there.
    private const string ToolInstanceRecipe = """
        {
          "name": "AIToolInstances",
          "instances": [
            {
              "Source": "http-api-request",
              "Name": "Order Lookup API",
              "Description": "Looks up a customer's order status by order identifier.",
              "Properties": {
                "HttpApiRequestToolSettings": {
                  "BaseUrl": "https://api.example.com/orders"
                }
              }
            }
          ]
        }
        """;

    [Fact]
    public async Task AIProviderConnectionsStep_ShouldImportTheDocumentedOpenAIRecipe()
    {
        // Arrange
        var step = GetStep(OpenAIProviderRecipe, AIProviderConnectionsStep.StepKey);
        var manager = new Mock<INamedSourceCatalogManager<AIProviderConnection>>();
        JsonNode created = null;
        manager
            .Setup(x => x.NewAsync("OpenAI", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) =>
            {
                created = data;

                return ValueTask.FromResult(new AIProviderConnection { Source = source, Name = data[nameof(AIProviderConnection.Name)]!.GetValue<string>() });
            });
        manager
            .Setup(x => x.ValidateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = new AIOptions();
        options.AddConnectionSource("OpenAI", _ => { });
        var handler = new AIProviderConnectionsStep(manager.Object, Options.Create(options), Policy(), new PassThroughLocalizer<AIProfileStep>());
        var context = CreateContext(step);

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        Assert.Equal("deepseek", created![nameof(AIProviderConnection.Name)]!.GetValue<string>());
        manager.Verify(x => x.CreateAsync(It.Is<AIProviderConnection>(c => c.Name == "deepseek" && c.Source == "OpenAI"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIDeploymentStep_ShouldImportEveryDeploymentOfTheDocumentedOpenAIRecipe()
    {
        // Arrange
        var step = GetStep(OpenAIProviderRecipe, AIDeploymentStep.StepKey);
        var manager = CreateDeploymentManager();
        var handler = new AIDeploymentStep(manager.Object, Options.Create(CreateDeploymentOptions("OpenAI")), Policy(), new PassThroughLocalizer<AIDeploymentStep>());
        var context = CreateContext(step);

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.Is<AIDeployment>(d => d.Name == "deepseek-chat" && d.Source == "OpenAI"), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.Is<AIDeployment>(d => d.Name == "deepseek-reasoner" && d.Source == "OpenAI"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIDeploymentStep_WhenOneDeploymentHasAnUnknownProvider_ShouldStillImportTheOthers()
    {
        // Arrange
        var manager = CreateDeploymentManager();
        var handler = new AIDeploymentStep(manager.Object, Options.Create(CreateDeploymentOptions("OpenAI")), Policy(), new PassThroughLocalizer<AIDeploymentStep>());
        var context = CreateContext(new JsonObject
        {
            ["name"] = AIDeploymentStep.StepKey,
            ["Deployments"] = new JsonArray
            {
                new JsonObject { ["Name"] = "first", ["ClientName"] = "Unknown" },
                new JsonObject { ["Name"] = "second", ["ClientName"] = "OpenAI" },
            },
        });

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);
        manager.Verify(x => x.CreateAsync(It.Is<AIDeployment>(d => d.Name == "second"), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.Is<AIDeployment>(d => d.Name == "first"), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIToolInstanceStep_ShouldImportTheDocumentedRecipe()
    {
        // Arrange
        var manager = new Mock<ISourceCatalogManager<AIToolInstance>>();
        manager
            .Setup(x => x.NewAsync("http-api-request", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) => ValueTask.FromResult(new AIToolInstance
            {
                Source = source,
                Name = data[nameof(AIToolInstance.Name)]!.GetValue<string>(),
            }));
        manager
            .Setup(x => x.ValidateAsync(It.IsAny<AIToolInstance>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = new AIOptions();
        options.AddToolInstanceSource("http-api-request", _ => { });
        var handler = new AIToolInstanceStep(manager.Object, Mock.Of<INamedCatalog<AIToolInstance>>(), Options.Create(options), new PassThroughLocalizer<AIToolInstanceStep>());
        var context = CreateContext(JsonNode.Parse(ToolInstanceRecipe)!.AsObject());

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.Is<AIToolInstance>(i => i.Name == "Order Lookup API"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIDeploymentDeploymentSource_ShouldExportWhatAIDeploymentStepImports()
    {
        // Arrange
        var deployment = new AIDeployment
        {
            ItemId = "4k2xq7a9m3bvt8c5d1e6f0gh2j",
            Name = "gpt-4o",
            ModelName = "gpt-4o-2024",
            Source = "OpenAI",
            ConnectionName = "default",
        };

        var catalog = new Mock<INamedSourceCatalog<AIDeployment>>();
        catalog
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIDeployment>>([deployment, new AIDeployment { Name = "skipped", Source = "OpenAI" }]));

        var source = new AIDeploymentDeploymentSource(catalog.Object);
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        var manager = CreateDeploymentManager();
        var handler = new AIDeploymentStep(manager.Object, Options.Create(CreateDeploymentOptions("OpenAI")), Policy(), new PassThroughLocalizer<AIDeploymentStep>());

        // Act
        await source.ProcessDeploymentStepAsync(new AIDeploymentDeploymentStep { DeploymentNames = ["gpt-4o"] }, result);

        var exported = Assert.Single(result.Steps);
        var context = CreateContext(exported);
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Equal(AIDeploymentStep.StepKey, exported["name"]!.GetValue<string>());
        var entry = Assert.Single(exported["Deployments"]!.AsArray())!;
        Assert.Equal("OpenAI", entry["ClientName"]!.GetValue<string>());
        Assert.Equal("OpenAI", entry["ProviderName"]!.GetValue<string>());
        Assert.Equal("gpt-4o-2024", entry["ModelName"]!.GetValue<string>());
        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.Is<AIDeployment>(d => d.Name == "gpt-4o" && d.Source == "OpenAI" && d.ItemId == deployment.ItemId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AIDataSourceDeploymentSource_ShouldKeepTheSourceOfAnExternalDataSource()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ItemId = "7m3bvt8c5d1e6f0gh2j4k2xq7a",
            Source = "AzureAISearch",
            DisplayText = "Products",
        };

        var store = new Mock<IAIDataSourceStore>();
        store
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult<IReadOnlyCollection<AIDataSource>>([dataSource]));

        var source = new AIDataSourceDeploymentSource(store.Object);
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        var manager = new Mock<ISourceCatalogManager<AIDataSource>>();
        manager
            .Setup(x => x.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string sourceName, JsonNode _, CancellationToken _) => ValueTask.FromResult(new AIDataSource { Source = sourceName }));
        manager
            .Setup(x => x.ValidateAsync(It.IsAny<AIDataSource>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = new AIDataSourceSourceOptions();
        options.AddOrUpdate("SearchIndexProfile", new("Search Index Profile", "Search Index Profile"), new("Index profile.", "Index profile."));
        options.AddOrUpdate("AzureAISearch", new("Azure AI Search", "Azure AI Search"), new("Azure AI Search index.", "Azure AI Search index."));
        var handler = new AIDataSourceStep(manager.Object, Options.Create(options), new PassThroughLocalizer<AIDataSourceStep>());

        // Act
        await source.ProcessDeploymentStepAsync(new AIDataSourceDeploymentStep { IncludeAll = true }, result);

        var exported = Assert.Single(result.Steps);
        var context = CreateContext(exported);
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Equal(AIDataSourceStep.StepKey, exported["name"]!.GetValue<string>());
        Assert.Empty(context.Errors);
        manager.Verify(x => x.NewAsync("AzureAISearch", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.Is<AIDataSource>(d => d.Source == "AzureAISearch" && d.ItemId == dataSource.ItemId), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IAIDeploymentManager> CreateDeploymentManager()
    {
        var manager = new Mock<IAIDeploymentManager>();
        manager
            .Setup(x => x.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) => ValueTask.FromResult(new AIDeployment
            {
                Source = source,
                Name = data[nameof(AIDeployment.Name)]?.GetValue<string>(),
            }));
        manager
            .Setup(x => x.ValidateAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        return manager;
    }

    private static AIOptions CreateDeploymentOptions(string source)
    {
        var options = new AIOptions();
        options.AddDeploymentProvider(source, _ => { });

        return options;
    }

    private static JsonObject GetStep(string recipe, string name)
        => JsonNode.Parse(recipe)!["steps"]!.AsArray()
            .Select(step => step!.AsObject())
            .Single(step => step["name"]!.GetValue<string>() == name);

    [Fact]
    public async Task AIProviderConnectionsStep_RefusesAConnectionWhoseEndpointIsOnTheSitesOwnNetwork()
    {
        // Arrange: the editor refuses this endpoint; a recipe could still save it and have the server call it.
        var step = GetStep(OpenAIProviderRecipe, AIProviderConnectionsStep.StepKey);
        var manager = new Mock<INamedSourceCatalogManager<AIProviderConnection>>();
        manager
            .Setup(x => x.NewAsync("OpenAI", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) =>
            {
                var connection = new AIProviderConnection { Source = source, Name = data[nameof(AIProviderConnection.Name)]!.GetValue<string>() };

                connection.Put(new RecipeEndpointMetadata { Endpoint = "http://169.254.169.254/latest/meta-data/" });

                return ValueTask.FromResult(connection);
            });
        manager
            .Setup(x => x.ValidateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = new AIOptions();
        options.AddConnectionSource("OpenAI", _ => { });
        var handler = new AIProviderConnectionsStep(manager.Object, Options.Create(options), Policy(), new PassThroughLocalizer<AIProfileStep>());
        var context = CreateContext(step);

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIDeploymentStep_RefusesADeploymentWhoseEndpointIsOnTheSitesOwnNetwork()
    {
        // Arrange
        var step = GetStep(OpenAIProviderRecipe, AIDeploymentStep.StepKey);
        var manager = CreateDeploymentManager();
        manager
            .Setup(x => x.NewAsync(It.IsAny<string>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((string source, JsonNode data, CancellationToken _) =>
            {
                var deployment = new AIDeployment { Source = source, Name = data[nameof(AIDeployment.Name)]!.GetValue<string>() };

                deployment.Properties = new Dictionary<string, object> { ["Endpoint"] = "http://10.0.0.5/speech" };

                return ValueTask.FromResult(deployment);
            });
        var handler = new AIDeploymentStep(manager.Object, Options.Create(CreateDeploymentOptions("OpenAI")), Policy(), new PassThroughLocalizer<AIDeploymentStep>());
        var context = CreateContext(step);

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The policy the AI feature registers, on a production host that allows no private network.
    private static CrestApps.OrchardCore.AI.Core.AIEndpointPolicy Policy()
        => new(Mock.Of<Microsoft.Extensions.Hosting.IHostEnvironment>(), Options.Create(new CrestApps.OrchardCore.AI.Core.AIEgressOptions()));

    private sealed class RecipeEndpointMetadata
    {
        public string Endpoint { get; set; }
    }

    private static RecipeExecutionContext CreateContext(JsonObject step)
        => new()
        {
            Name = step["name"]!.GetValue<string>(),
            Step = step,
        };

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
