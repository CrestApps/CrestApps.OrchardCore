using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage;
using OrchardCore.Json;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Recipes;

/// <summary>
/// SEC-A3: the recipe tools run for whoever drives the chat. A tool gated by a narrower permission than
/// <c>ManageRecipes</c> may import only its own steps, and the tool that runs a named recipe needs <c>ManageRecipes</c>.
/// </summary>
public sealed class RecipeToolScopeTests
{
    [Fact]
    public async Task ContentTypeTool_RefusesARecipeWithARolesStep()
    {
        var services = CreateRecipeServices();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["recipe"] = """{"steps":[{"name":"ContentDefinition","ContentTypes":[]},{"name":"Roles","Roles":[]}]}""",
        })
        {
            Services = services,
        };

        var result = await new CreateOrUpdateContentTypeDefinitionsTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

        var text = Assert.IsType<string>(result);
        Assert.StartsWith("This tool can only import", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ContentTypeTool_ImportsAContentDefinitionStep()
    {
        var services = CreateRecipeServices();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["recipe"] = """{"steps":[{"name":"ContentDefinition","ContentTypes":[]}]}""",
        })
        {
            Services = services,
        };

        var result = await new CreateOrUpdateContentTypeDefinitionsTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.Equal("Recipe was successfully imported", Assert.IsType<string>(result));
    }

    [Fact]
    public async Task WorkflowTool_RefusesARecipeWithASettingsStep()
    {
        var services = CreateRecipeServices();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["recipe"] = """{"steps":[{"name":"WorkflowType","data":[]},{"name":"Settings"}]}""",
        })
        {
            Services = services,
        };

        var result = await new CreateOrUpdateWorkflowTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

        var text = Assert.IsType<string>(result);
        Assert.StartsWith("This tool can only import", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteNonStartupRecipe_WithAnAnonymousCaller_RunsNothing()
    {
        var recipe = new RecipeDescriptor { Name = "blog", DisplayName = "Blog", BasePath = "Modules/Blog" };
        var harvester = new Mock<IRecipeHarvester>();
        harvester.Setup(x => x.HarvestRecipesAsync()).ReturnsAsync([recipe]);
        var executor = new Mock<IRecipeExecutor>();
        var shellHost = new Mock<IShellHost>();

        var services = new ServiceCollection()
            .AddSingleton<IEnumerable<IRecipeHarvester>>([harvester.Object])
            .AddSingleton<IEnumerable<IRecipeEnvironmentProvider>>([])
            .AddSingleton(executor.Object)
            .AddSingleton(Mock.Of<IShellFeaturesManager>())
            .AddSingleton(shellHost.Object)
            .AddSingleton(new ShellSettings())
            .AddSingleton<ILogger<ExecuteStartupRecipesTool>>(NullLogger<ExecuteStartupRecipesTool>.Instance)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() })
            .BuildServiceProvider();

        var arguments = new AIFunctionArguments(new Dictionary<string, object> { ["recipeName"] = "blog" })
        {
            Services = services,
        };

        var result = await new ExecuteStartupRecipesTool().InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        executor.VerifyNoOtherCalls();
        shellHost.VerifyNoOtherCalls();
    }

    private static ServiceProvider CreateRecipeServices()
    {
        IRecipeStep[] steps = [CreateStep("ContentDefinition"), CreateStep("Roles"), CreateStep("WorkflowType"), CreateStep("Settings")];
        var tempDirectoryProvider = new Mock<ITempDirectoryProvider>();
        tempDirectoryProvider
            .Setup(provider => provider.GetTempFileName(It.IsAny<string>()))
            .Returns((string extension) => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + (extension ?? string.Empty)));

        return new ServiceCollection()
            .AddSingleton(new RecipeSchemaService(steps))
            .AddSingleton(new RecipeExecutionService([], Options.Create(new DocumentJsonSerializerOptions()), tempDirectoryProvider.Object, NullLogger<RecipeExecutionService>.Instance))
            .AddSingleton<ILogger<CreateOrUpdateContentTypeDefinitionsTool>>(NullLogger<CreateOrUpdateContentTypeDefinitionsTool>.Instance)
            .AddSingleton<ILogger<CreateOrUpdateWorkflowTool>>(NullLogger<CreateOrUpdateWorkflowTool>.Instance)
            .AddAuthorizedCaller()
            .BuildServiceProvider();
    }

    private static IRecipeStep CreateStep(string name)
    {
        var step = new Mock<IRecipeStep>();
        step.SetupGet(x => x.Name).Returns(name);
        step.Setup(x => x.GetSchemaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(name)))
                .Required("name")
                .AdditionalProperties(true)
                .Build());

        return step.Object;
    }
}
