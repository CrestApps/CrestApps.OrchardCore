using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Products;
using CrestApps.OrchardCore.Products.Drivers;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Handlers;
using ProductsAIToolsStartup = CrestApps.OrchardCore.Products.AIToolsStartup;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Products.AI;

/// <summary>
/// The Products module exposes the currencies to the generic record tools as the <c>currency</c> kind, with the
/// Currencies page's permission and named catalog manager. A currency saved from the chat goes through the Currencies
/// editor's own driver, and the page's refusal of a code another currency has still applies.
/// </summary>
public sealed class CurrencyRecordKindTests
{
    private readonly InMemoryRecordManager<CurrencyEntry> _currencies = new();

    public CurrencyRecordKindTests()
    {
        _currencies.Items.Add(new CurrencyEntry
        {
            ItemId = "usd",
            Name = "USD",
            DisplayName = "US dollar",
        });
    }

    [Fact]
    public void TheProductsStartup_RegistersTheCurrencyKind_WithTheCurrenciesPermission()
    {
        // Arrange
        var services = new ServiceCollection();

        new ProductsAIToolsStartup(new RecordTestLocalizer<ProductsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;

        // Assert
        var kind = Assert.Single(kinds.Values);

        Assert.Equal("currency", kind.Name);
        Assert.Equal(typeof(CurrencyEntry), kind.RecordType);
        Assert.Equal(ProductsConstants.Permissions.ManageCurrencies.Name, kind.Permission.Name);
        Assert.Equal(ProductsConstants.Permissions.ManageCurrencies.Name, kind.EffectiveReadPermission.Name);
        Assert.True(kind.CanWrite);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
    }

    [Fact]
    public async Task ListRecords_ReturnsOnlyTheCodeAndTheDisplayName()
    {
        // Arrange
        using var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRecordsTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "currency",
        });

        // Assert
        var item = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("items").EnumerateArray());

        Assert.Equal("usd", item.GetProperty("id").GetString());
        Assert.Equal("USD", item.GetProperty("code").GetString());
        Assert.Equal("US dollar", item.GetProperty("displayName").GetString());
        Assert.False(item.TryGetProperty("author", out _));
        Assert.False(item.TryGetProperty("ownerId", out _));
    }

    [Fact]
    public async Task SaveRecord_CreatesACurrency_ThroughTheCurrenciesEditor()
    {
        // Arrange
        using var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "currency",
            ["values"] = JsonNode.Parse("""{ "Name": " eur ", "DisplayName": " Euro " }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = Assert.Single(_currencies.Created);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("EUR", created.Name);
        Assert.Equal("Euro", created.DisplayName);
        Assert.Equal("EUR", json.GetProperty("record").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SaveRecord_RefusesACodeAnotherCurrencyHas_AsTheCurrenciesPageDoes()
    {
        // Arrange: the handlers accept the currency, so the refusal comes from the page's own check.
        _currencies.Validate = _ => new ValidationResultDetails();

        using var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "currency",
            ["values"] = JsonNode.Parse("""{ "Name": "usd", "DisplayName": "Dollar" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var error = Assert.Single(json.GetProperty("errors").EnumerateArray());

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Name", error.GetProperty("field").GetString());
        Assert.Equal("A currency with the same code already exists.", error.GetProperty("message").GetString());
        Assert.Empty(_currencies.Created);
        Assert.Single(_currencies.Items);
    }

    [Fact]
    public async Task SaveRecord_RenamesACurrency_AndKeepsItsCode()
    {
        // Arrange
        using var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "currency",
            ["id"] = "usd",
            ["values"] = JsonNode.Parse("""{ "Name": "CAD", "DisplayName": "Dollar" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var stored = _currencies.Find("usd");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("USD", stored.Name);
        Assert.Equal("Dollar", stored.DisplayName);
        Assert.Equal("Name", Assert.Single(json.GetProperty("unchanged").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task DeleteRecord_DeletesACurrencyNoProductIsPricedIn()
    {
        // Arrange
        using var services = Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "currency",
            ["ids"] = new[] { "usd" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("usd", Assert.Single(json.GetProperty("deleted").EnumerateArray()).GetProperty("id").GetString());
        Assert.Empty(_currencies.Items);
    }

    private ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<AIRecordScopeChanges>();
        services.AddScoped<IDisplayDriver<CurrencyEntry>, CurrencyEntryDisplayDriver>();
        services.AddSingleton<INamedCatalogManager<CurrencyEntry>>(_currencies);

        // No content type has a product part, so no product is priced in any currency.
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(Array.Empty<ContentTypeDefinition>());
        services.AddScoped(_ => new CurrencyUsageService(definitions.Object, Mock.Of<YesSqlSession>()));

        new ProductsAIToolsStartup(new RecordTestLocalizer<ProductsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }
}
