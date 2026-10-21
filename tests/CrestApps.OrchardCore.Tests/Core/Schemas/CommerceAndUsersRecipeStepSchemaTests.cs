using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Services;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Deployments;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Users.Core;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// Pins the recipe step schemas of the Customers, Inventory, Stripe and Users features to the steps they describe, and
/// checks each is registered under the feature that registers its step, so the AI agent's recipe tools list the step
/// exactly when the tenant can run it.
/// </summary>
public sealed class CommerceAndUsersRecipeStepSchemaTests
{
    /// <summary>
    /// Describes one schema, the startup that registers it, the features that startup requires and the step name the
    /// step handler is registered under.
    /// </summary>
    private sealed record StepCase(Type SchemaType, IStartup Startup, string[] RequiredFeatures, string HandlerStepName);

    private static readonly StepCase[] _steps =
    [
        new(typeof(CustomersRecipeStep), new CustomersRecipeStartup(), [CustomersConstants.Features.Customers], CustomersConstants.Recipes.Customers),
        new(typeof(StockRecipeStep), new InventoryRecipeStartup(), [InventoryConstants.Features.Inventory], InventoryConstants.StockRecipeStep),
        new(typeof(StripeSettingsRecipeStep), new StripeRecipeStartup(), [StripeConstants.Feature.ModuleId], StripeDeploymentSteps.Settings),

        // The handler passes the literal "IndexUsers" to its base and is registered by the CrestApps Users feature
        // when Orchard Core Users is enabled.
        new(typeof(IndexUsersRecipeStep), new CrestAppsUsersRecipeStartup(), [UsersConstants.Feature.Area, "OrchardCore.Users"], "IndexUsers"),
    ];

    public static TheoryData<string> HandlerStepNames()
        => [.. _steps.Select(step => step.HandlerStepName)];

    [Theory]
    [MemberData(nameof(HandlerStepNames))]
    public void Startup_RequiresTheFeatureThatRegistersTheStep(string stepName)
    {
        // Arrange
        var step = _steps.Single(candidate => candidate.HandlerStepName == stepName);

        // Act
        var requiredFeatures = RequireFeaturesAttribute.GetRequiredFeatureNamesForType(step.Startup.GetType());

        // Assert
        Assert.Equal(step.RequiredFeatures.Order(StringComparer.Ordinal), requiredFeatures.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(HandlerStepNames))]
    public async Task EnabledFeature_ListsTheStepWithItsSchema(string stepName)
    {
        // Arrange
        var step = _steps.Single(candidate => candidate.HandlerStepName == stepName);
        await using var services = CreateServices(step.Startup);
        await using var scope = services.CreateAsyncScope();
        var recipeSchemaService = scope.ServiceProvider.GetRequiredService<RecipeSchemaService>();

        // Act
        var stepNames = recipeSchemaService.GetStepNames();
        var registered = Assert.Single(scope.ServiceProvider.GetServices<IRecipeStep>());
        var schema = ToNode(await registered.GetSchemaAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([stepName], stepNames.ToArray());
        Assert.IsType(step.SchemaType, registered);
        Assert.Equal(stepName, schema["properties"]?["name"]?["const"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetRecipeJsonSchemaTool_WithEveryFeatureEnabled_DescribesEachStep()
    {
        // Arrange
        await using var services = CreateServices([.. _steps.Select(step => step.Startup)]);
        await using var scope = services.CreateAsyncScope();
        var tool = new GetRecipeJsonSchemaTool();

        foreach (var step in _steps)
        {
            var arguments = new AIFunctionArguments(new Dictionary<string, object>
            {
                ["step"] = step.HandlerStepName,
            })
            {
                Services = scope.ServiceProvider,
            };

            // Act
            var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

            // Assert
            var text = Assert.IsType<string>(result);
            using var document = JsonDocument.Parse(text);
            var stepItems = document.RootElement.GetProperty("properties").GetProperty("steps").GetProperty("items");
            var definition = Assert.Single(stepItems.GetProperty("allOf").EnumerateArray());

            Assert.Equal(step.HandlerStepName, definition.GetProperty("if").GetProperty("properties").GetProperty("name").GetProperty("const").GetString());
        }
    }

    [Fact]
    public async Task StripeSettingsSchema_DescribesEveryImportedSetting()
    {
        // Arrange
        var root = ToNode(await new StripeSettingsRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken));
        var problems = new List<string>();

        // Act
        Assert.Contains("Settings", root["required"]!.AsArray().Select(node => node.GetValue<string>()));

        var settings = root["properties"]!["Settings"]!.AsObject();

        // The step ignores the members that describe the source site's connection to Stripe.
        ContactCenterRecipeSchemaCoverage.CheckObject(typeof(StripeSettings), settings, "StripeSettings.Settings", problems, ignored: StripeDeploymentSteps.EnvironmentOwnedMembers);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend("StripeSettings is out of date with StripeSettings:")));

        foreach (var member in StripeDeploymentSteps.EnvironmentOwnedMembers)
        {
            Assert.Null(settings["properties"]![member]);
        }
    }

    [Fact]
    public async Task CustomersSchema_DescribesEveryMemberTheStepImports()
    {
        // Arrange
        var root = ToNode(await new CustomersRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken));
        var problems = new List<string>();

        // Act
        Assert.Contains("Customers", root["required"]!.AsArray().Select(node => node.GetValue<string>()));

        var items = root["properties"]!["Customers"]!["items"]!.AsObject();

        ContactCenterRecipeSchemaCoverage.CheckObject(
            typeof(Customer),
            items,
            "Customers.Customers[]",
            problems,
            only:
            [
                nameof(Customer.ItemId),
                nameof(Customer.Kind),
                nameof(Customer.DisplayName),
                nameof(Customer.FirstName),
                nameof(Customer.LastName),
                nameof(Customer.CompanyName),
                nameof(Customer.TaxId),
                nameof(Customer.Email),
                nameof(Customer.Phone),
                nameof(Customer.AcceptsMarketing),
                nameof(Customer.Addresses),
                nameof(Customer.DefaultBillingAddressId),
                nameof(Customer.DefaultShippingAddressId),
            ]);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend("Customers is out of date with Customer:")));
    }

    [Fact]
    public async Task StockSchema_DescribesTheCountRow()
    {
        // Arrange
        var root = ToNode(await new StockRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken));

        // Act
        var items = root["properties"]!["Items"]!["items"]!.AsObject();

        // Assert
        Assert.Contains("Items", root["required"]!.AsArray().Select(node => node.GetValue<string>()));
        Assert.Equal(["ContentItemId", "OnHand", "Sku", "VariantId"], items["properties"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["OnHand"], items["required"]!.AsArray().Select(node => node.GetValue<string>()));
        Assert.Equal("integer", items["properties"]!["OnHand"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task IndexUsersSchema_DescribesTheStepOptions()
    {
        // Arrange
        var root = ToNode(await new IndexUsersRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken));

        // Act
        var properties = root["properties"]!.AsObject();

        // Assert
        Assert.Equal(["BatchSize", "IncludeDisabledUsers", "name"], properties.Select(property => property.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["name"], root["required"]!.AsArray().Select(node => node.GetValue<string>()));
    }

    private static ServiceProvider CreateServices(params IStartup[] startups)
    {
        var services = new ServiceCollection()
            .AddMemoryCache()
            .AddScoped<RecipeSchemaService>()
            .AddSingleton<ILogger<GetRecipeJsonSchemaTool>>(NullLogger<GetRecipeJsonSchemaTool>.Instance);

        foreach (var startup in startups)
        {
            startup.ConfigureServices(services);
        }

        return services.BuildServiceProvider();
    }

    private static JsonObject ToNode(JsonSchema schema)
        => JsonNode.Parse(schema.Root.Source.GetRawText())!.AsObject();
}
