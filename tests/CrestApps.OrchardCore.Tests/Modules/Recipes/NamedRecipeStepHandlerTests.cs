using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Recipes;

public sealed class NamedRecipeStepHandlerTests
{
    [Fact]
    public async Task AIProfileStep_WhenProfileExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var profile = new AIProfile
        {
            ItemId = "profile-1",
            Name = "support",
        };

        var manager = new Mock<IAIProfileManager>();
        manager.Setup(x => x.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(profile));
        manager.Setup(x => x.ValidateAsync(profile, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.AIProfileStep, CrestApps.OrchardCore.AI",
            manager.Object,
            null);

        var context = CreateContext("AIProfile", new JsonObject
        {
            ["Profiles"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AIProfile.ItemId)] = "profile-1",
                    [nameof(AIProfile.Name)] = "support",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(profile, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIProfileTemplateStep_WhenTemplateExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            ItemId = "template-1",
            Name = "customer-support",
        };

        var manager = new Mock<INamedCatalogManager<AIProfileTemplate>>();
        manager.Setup(x => x.FindByIdAsync("template-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(template));
        manager.Setup(x => x.ValidateAsync(template, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.AIProfileTemplateStep, CrestApps.OrchardCore.AI",
            manager.Object,
            null);

        var context = CreateContext("AIProfileTemplate", new JsonObject
        {
            ["Templates"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AIProfileTemplate.ItemId)] = "template-1",
                    [nameof(AIProfileTemplate.Name)] = "customer-support",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(template, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIProfileTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIProviderConnectionsStep_WhenConnectionExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var connection = new AIProviderConnection
        {
            ItemId = "connection-1",
            Name = "openai",
            Source = "openai",
        };

        var manager = new Mock<INamedSourceCatalogManager<AIProviderConnection>>();
        manager.Setup(x => x.FindByIdAsync("connection-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(connection));
        manager.Setup(x => x.ValidateAsync(connection, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.AIProviderConnectionsStep, CrestApps.OrchardCore.AI",
            manager.Object,
            Options.Create(new AIOptions()),
            null);

        var context = CreateContext("AIProviderConnections", new JsonObject
        {
            ["Connections"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AIProviderConnection.ItemId)] = "connection-1",
                    [nameof(AIProviderConnection.Name)] = "openai",
                    [nameof(AIProviderConnection.Source)] = "openai",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(connection, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIDeploymentStep_WhenDeploymentExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var deployment = new AIDeployment
        {
            ItemId = "deployment-1",
            Name = "default-chat",
            ClientName = "openai",
        };

        var manager = new Mock<IAIDeploymentManager>();
        manager.Setup(x => x.FindByIdAsync("deployment-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(deployment));
        manager.Setup(x => x.ValidateAsync(deployment, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.AIDeploymentStep, CrestApps.OrchardCore.AI",
            manager.Object,
            Options.Create(new AIOptions()),
            null);

        var context = CreateContext("AIDeployment", new JsonObject
        {
            ["Deployments"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AIDeployment.ItemId)] = "deployment-1",
                    [nameof(AIDeployment.Name)] = "default-chat",
                    [nameof(AIDeployment.ClientName)] = "openai",
                    [nameof(AIDeployment.Purpose)] = nameof(AIDeploymentPurpose.Chat),
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(deployment, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIDeployment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAIDeploymentStep_WhenDeploymentExists_ShouldDeleteIt()
    {
        // Arrange
        var deployment = new AIDeployment
        {
            ItemId = "deployment-1",
            Name = "default-chat",
        };

        var manager = new Mock<IAIDeploymentManager>();
        manager.Setup(x => x.FindByNameAsync("default-chat", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(deployment));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.DeleteAIDeploymentStep, CrestApps.OrchardCore.AI",
            manager.Object,
            null);

        var context = CreateContext("DeleteAIDeployments", new JsonObject
        {
            ["IncludeAll"] = false,
            ["DeploymentNames"] = new JsonArray("default-chat"),
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.DeleteAsync(deployment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAIProfileFromTemplateStep_WhenProfileExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var template = new AIProfileTemplate
        {
            ItemId = "template-1",
            Name = "support-template",
            Source = AITemplateSources.Profile,
        };

        var profile = new AIProfile
        {
            ItemId = "profile-1",
            Name = "support",
        };

        var profileManager = new Mock<IAIProfileManager>();
        profileManager.Setup(x => x.FindByNameAsync("support", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(profile));
        profileManager.Setup(x => x.ValidateAsync(profile, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var templateManager = new Mock<INamedCatalogManager<AIProfileTemplate>>();
        templateManager.Setup(x => x.FindByIdAsync("template-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(template));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Recipes.CreateAIProfileFromTemplateStep, CrestApps.OrchardCore.AI",
            profileManager.Object,
            templateManager.Object,
            null);

        var context = CreateContext("CreateAIProfileFromTemplate", new JsonObject
        {
            ["Profiles"] = new JsonArray
            {
                new JsonObject
                {
                    ["TemplateId"] = "template-1",
                    [nameof(AIProfile.Name)] = "support",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        profileManager.Verify(x => x.UpdateAsync(profile, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        profileManager.Verify(x => x.CreateAsync(It.IsAny<AIProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIDataSourceStep_WhenDataSourceExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var dataSource = new AIDataSource
        {
            ItemId = "data-source-1",
        };

        var manager = new Mock<ISourceCatalogManager<AIDataSource>>();
        manager.Setup(x => x.FindByIdAsync("data-source-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(dataSource));
        manager.Setup(x => x.ValidateAsync(dataSource, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = CreateAIDataSourceSourceOptions();
        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.DataSources.Recipes.AIDataSourceStep, CrestApps.OrchardCore.AI.DataSources",
            manager.Object,
            options,
            null);

        var context = CreateContext("AIDataSource", new JsonObject
        {
            ["DataSources"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AIDataSource.ItemId)] = "data-source-1",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(dataSource, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AIDataSource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIDataSourceStep_WhenDataSourceIsNew_ShouldUseRecipeSourceValue()
    {
        // Arrange
        var token = new JsonObject
        {
            [nameof(AIDataSource.DisplayText)] = "Products",
            [nameof(AIDataSource.Source)] = "PostgreSQL",
        };

        var dataSource = new AIDataSource
        {
            DisplayText = "Products",
            Source = "PostgreSQL",
        };

        var manager = new Mock<ISourceCatalogManager<AIDataSource>>();
        manager.Setup(x => x.NewAsync("PostgreSQL", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(dataSource));
        manager.Setup(x => x.ValidateAsync(dataSource, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = CreateAIDataSourceSourceOptions();
        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.DataSources.Recipes.AIDataSourceStep, CrestApps.OrchardCore.AI.DataSources",
            manager.Object,
            options,
            null);

        var context = CreateContext("AIDataSource", new JsonObject
        {
            ["DataSources"] = new JsonArray
            {
                token,
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.NewAsync("PostgreSQL", It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(dataSource, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AgentStateReasonCodeStep_WhenReasonCodeExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var reasonCode = new AgentStateReasonCode
        {
            ItemId = "reason-1",
            Name = "Lunch",
        };

        var manager = new Mock<IAgentStateReasonCodeManager>();
        manager.Setup(x => x.FindByIdAsync("reason-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(reasonCode));
        manager.Setup(x => x.ValidateAsync(reasonCode, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.ContactCenter.Recipes.AgentStateReasonCodeStep, CrestApps.OrchardCore.ContactCenter",
            manager.Object,
            null);

        var context = CreateContext("AgentStateReasonCode", new JsonObject
        {
            ["ReasonCodes"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AgentStateReasonCode.ItemId)] = "reason-1",
                    [nameof(AgentStateReasonCode.Name)] = "Lunch",
                    [nameof(AgentStateReasonCode.AppliesTo)] = "Break",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(reasonCode, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AgentStateReasonCodeStep_WhenReasonCodeIsNew_ShouldCreate()
    {
        // Arrange
        var manager = new Mock<IAgentStateReasonCodeManager>();
        manager.Setup(x => x.FindByNameAsync("Lunch", It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult<AgentStateReasonCode>(null));
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new AgentStateReasonCode { Name = "Lunch" }));
        manager.Setup(x => x.ValidateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.ContactCenter.Recipes.AgentStateReasonCodeStep, CrestApps.OrchardCore.ContactCenter",
            manager.Object,
            null);

        var context = CreateContext("AgentStateReasonCode", new JsonObject
        {
            ["ReasonCodes"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(AgentStateReasonCode.Name)] = "Lunch",
                    [nameof(AgentStateReasonCode.AppliesTo)] = "Break",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.UpdateAsync(It.IsAny<AgentStateReasonCode>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task McpConnectionStep_WhenConnectionExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var connection = new McpConnection
        {
            ItemId = "mcp-connection-1",
            Source = "stdio",
        };

        var manager = new Mock<ISourceCatalogManager<McpConnection>>();
        manager.Setup(x => x.FindByIdAsync("mcp-connection-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(connection));
        manager.Setup(x => x.ValidateAsync(connection, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var options = Options.Create(new McpClientAIOptions());

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Mcp.Recipes.McpConnectionStep, CrestApps.OrchardCore.AI.Mcp",
            manager.Object,
            options,
            null);

        var context = CreateContext("McpConnection", new JsonObject
        {
            ["Connections"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(McpConnection.ItemId)] = "mcp-connection-1",
                    [nameof(McpConnection.Source)] = "stdio",
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(connection, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task McpPromptStep_WhenPromptExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var prompt = new McpPrompt
        {
            ItemId = "prompt-1",
            Name = "summarize",
        };

        var manager = new Mock<INamedCatalogManager<McpPrompt>>();
        manager.Setup(x => x.FindByIdAsync("prompt-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(prompt));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Mcp.Recipes.McpPromptStep, CrestApps.OrchardCore.AI.Mcp",
            manager.Object,
            null);

        var context = CreateContext("McpPrompt", new JsonObject
        {
            ["Prompts"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(McpPrompt.ItemId)] = "prompt-1",
                    [nameof(McpPrompt.Prompt)] = new JsonObject
                    {
                        [nameof(McpPrompt.Name)] = "summarize",
                    },
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(prompt, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<McpPrompt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task McpResourceStep_WhenResourceExists_ShouldUpdateInsteadOfCreate()
    {
        // Arrange
        var resource = new McpResource
        {
            ItemId = "resource-1",
            Source = "ftp",
        };

        var manager = new Mock<ISourceCatalogManager<McpResource>>();
        manager.Setup(x => x.FindByIdAsync("resource-1", It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(resource));

        var handler = CreateHandler(
            "CrestApps.OrchardCore.AI.Mcp.Recipes.McpResourceStep, CrestApps.OrchardCore.AI.Mcp",
            manager.Object,
            null);

        var context = CreateContext("McpResource", new JsonObject
        {
            ["Resources"] = new JsonArray
            {
                new JsonObject
                {
                    [nameof(McpResource.ItemId)] = "resource-1",
                    [nameof(McpResource.DisplayText)] = "Product docs",
                    [nameof(McpResource.Resource)] = new JsonObject
                    {
                        ["Uri"] = "https://example.com/docs",
                        ["Name"] = "docs",
                    },
                },
            },
        });

        // Act
        await ExecuteAsync(handler, context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(x => x.UpdateAsync(resource, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(x => x.CreateAsync(It.IsAny<McpResource>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserRecipeStepHandler_WhenNoEnabledUsersExist_ShouldNotSaveAnything()
    {
        // Arrange
        var emptyBatch = Enumerable.Empty<User>();

        var query = new Mock<IQuery<User, UserIndex>>();
        query.Setup(x => x.OrderBy(It.IsAny<Expression<Func<UserIndex, object>>>()))
            .Returns(query.Object);
        query.Setup(x => x.Skip(It.IsAny<int>()))
            .Returns(query.Object);
        query.Setup(x => x.Take(It.IsAny<int>()))
            .Returns(query.Object);
        query.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyBatch);
        query.Setup(x => x.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var entityQuery = new Mock<IQuery<User>>();
        entityQuery.Setup(x => x.With<UserIndex>(It.IsAny<Expression<Func<UserIndex, bool>>>()))
            .Returns(query.Object);

        var rootQuery = new Mock<IQuery>();
        rootQuery.Setup(x => x.For<User>(false))
            .Returns(entityQuery.Object);

        var session = new Mock<ISession>();
        session.Setup(x => x.Query(null))
            .Returns(rootQuery.Object);

        var handler = new CrestApps.OrchardCore.Users.Recipes.UpdateUserRecipeStepHandler(session.Object);
        var context = CreateContext("IndexUsers", new JsonObject());

        // Act
        await handler.ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        session.Verify(x => x.SaveAsync(It.IsAny<object>(), false, null, It.IsAny<CancellationToken>()), Times.Never);
        session.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static object CreateHandler(string typeName, params object[] arguments)
    {
        var type = Type.GetType(typeName, throwOnError: true)!;
        var constructor = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(x => x.GetParameters().Length == arguments.Length);

        return constructor.Invoke(arguments);
    }

    private static RecipeExecutionContext CreateContext(string stepName, JsonObject step)
        => new()
        {
            Name = stepName,
            Step = step,
        };

    private static async Task ExecuteAsync(object handler, RecipeExecutionContext context)
    {
        var executeMethod = handler.GetType().GetMethod(nameof(global::OrchardCore.Recipes.Services.IRecipeStepHandler.ExecuteAsync))!;
        var task = (Task)executeMethod.Invoke(handler, [context])!;

        await task;
    }

    private static IOptions<AIDataSourceSourceOptions> CreateAIDataSourceSourceOptions()
    {
        var options = new AIDataSourceSourceOptions();
        options.AddOrUpdate("SearchIndexProfile", new("Search Index Profile", "Search Index Profile"), new("Read source documents from an Orchard-managed search index profile.", "Read source documents from an Orchard-managed search index profile."));
        options.AddOrUpdate("AzureAISearch", new("Azure AI Search", "Azure AI Search"), new("Read source documents from an external Azure AI Search index.", "Read source documents from an external Azure AI Search index."));
        options.AddOrUpdate("Elasticsearch", new("Elasticsearch", "Elasticsearch"), new("Read source documents from an external Elasticsearch index.", "Read source documents from an external Elasticsearch index."));
        options.AddOrUpdate("PostgreSQL", new("PostgreSQL", "PostgreSQL"), new("Read source documents from a PostgreSQL table.", "Read source documents from a PostgreSQL table."));

        return Options.Create(options);
    }
}
