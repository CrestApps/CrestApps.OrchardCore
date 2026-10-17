using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Recipes;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// The store sample adds its customers through a recipe step, and running the recipe again changes nothing.
/// </summary>
public sealed class CustomersRecipeStepTests
{
    private static readonly DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCustomerStore _store = new();

    private CustomersStep CreateStep()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        return new CustomersStep(_store, clock.Object, new PassThroughLocalizer());
    }

    private static RecipeExecutionContext Context(JsonObject step)
        => new() { Name = (string)step["name"], Step = step };

    private static JsonObject StoreSampleStep()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var recipe = JsonNode.Parse(File.ReadAllText(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Products", "Recipes", "product-listing-store.recipe.json")));

        Assert.Contains("CrestApps.OrchardCore.Customers", recipe["steps"][0]["enable"].AsArray().Select(feature => (string)feature));

        return recipe["steps"].AsArray().Single(step => (string)step["name"] == "Customers").AsObject();
    }

    [Fact]
    public async Task TheStoreSample_AddsItsCustomersWithTheirAddresses_Once()
    {
        var step = StoreSampleStep();

        await CreateStep().ExecuteAsync(Context(step));
        await CreateStep().ExecuteAsync(Context((JsonObject)step.DeepClone()));

        Assert.Equal(2, _store.Customers.Count);

        var person = _store.Customers.Single(customer => customer.Kind == CustomerKind.Person);
        Assert.Equal("Sam Carter", person.GetName());
        Assert.Equal(2, person.Addresses.Count);
        Assert.Equal("sample-sam-home", person.DefaultBillingAddressId);
        Assert.Equal("Seattle", person.FindAddress("sample-sam-home").Address.City);
        Assert.Equal(_now, person.LastActivityUtc);

        var business = _store.Customers.Single(customer => customer.Kind == CustomerKind.Business);
        Assert.Equal("Contoso Supplies", business.CompanyName);
        Assert.Equal("CA", business.FindAddress(business.DefaultBillingAddressId).Address.Country);
    }

    [Fact]
    public async Task ACustomerWithoutANameOrEmail_OrWithABadEmail_IsReported()
    {
        var context = Context(new JsonObject
        {
            ["name"] = "Customers",
            ["Customers"] = new JsonArray
            {
                new JsonObject { ["Phone"] = "555-0100" },
                new JsonObject { ["DisplayName"] = "Pat", ["Email"] = "not-an-email" },
            },
        });

        await CreateStep().ExecuteAsync(context);

        Assert.Empty(_store.Customers);
        Assert.Equal(2, context.Errors.Count);
    }

    [Fact]
    public async Task AnAccountsCustomer_IsNotChangedByARecipe()
    {
        _store.Customers.Add(new Customer { ItemId = "c1", UserId = "user-1", DisplayName = "Pat", Email = "pat@example.test" });

        await CreateStep().ExecuteAsync(Context(new JsonObject
        {
            ["name"] = "Customers",
            ["Customers"] = new JsonArray { new JsonObject { ["ItemId"] = "c1", ["DisplayName"] = "Someone else" } },
        }));

        Assert.Equal("Pat", Assert.Single(_store.Customers).DisplayName);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<CustomersStep>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
