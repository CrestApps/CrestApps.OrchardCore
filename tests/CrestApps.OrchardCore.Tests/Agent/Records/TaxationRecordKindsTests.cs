using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using TaxationAIToolsStartup = CrestApps.OrchardCore.Taxation.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Agent.Records;

/// <summary>
/// The Taxation module exposes tax categories, tax types and tax jurisdictions to the generic record tools with the
/// Taxation permission and named catalog managers, and records saved from the chat go through the Taxation admin
/// editors' own drivers.
/// </summary>
public sealed class TaxationRecordKindsTests
{
    [Fact]
    public void TheTaxationStartup_RegistersItsThreeKinds_WithTheTaxationPermission()
    {
        // Arrange
        var services = new ServiceCollection();

        new TaxationAIToolsStartup(new RecordTestLocalizer<TaxationAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;

        // Assert
        Assert.Equal(new[] { "taxCategory", "taxJurisdiction", "taxType" }, kinds.Keys.Order(StringComparer.Ordinal));

        foreach (var kind in kinds.Values)
        {
            Assert.Equal(TaxationPermissions.ManageTaxation.Name, kind.Permission.Name);
            Assert.Equal(TaxationPermissions.ManageTaxation.Name, kind.EffectiveReadPermission.Name);
            Assert.True(kind.CanWrite);
            Assert.False(string.IsNullOrWhiteSpace(kind.Description));
            Assert.NotEqual(kind.Name, kind.Title);
        }
    }

    [Fact]
    public async Task SaveRecord_CreatesATaxCategory_ThroughTheTaxationEditor()
    {
        // Arrange
        var categories = new InMemoryRecordManager<TaxCategory>();
        using var services = Build(collection =>
        {
            collection.AddScoped<IDisplayDriver<TaxCategory>, TaxCategoryDisplayDriver>();
            collection.AddSingleton<INamedCatalogManager<TaxCategory>>(categories);
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "taxCategory",
            ["values"] = JsonNode.Parse("""{ "Name": " Food ", "Code": " FOOD ", "Description": "Groceries" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = Assert.Single(categories.Created);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("Food", created.Name);
        Assert.Equal("FOOD", created.Code);
        Assert.Equal("Groceries", created.Description);
        Assert.Equal("FOOD", json.GetProperty("record").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteRecord_KeepsATaxCategoryThatATaxRuleUses()
    {
        // Arrange
        var categories = new InMemoryRecordManager<TaxCategory>();
        categories.Items.Add(new TaxCategory
        {
            ItemId = "category-1",
            Name = "Food",
            Code = "FOOD",
        });

        var rules = new Taxation.Fakes.InMemoryNamedCatalog<TaxRule>();
        await rules.CreateAsync(new TaxRule
        {
            ItemId = "rule-1",
            Name = "Food tax",
            CategoryCode = "food",
        }, TestContext.Current.CancellationToken);

        using var services = Build(collection =>
        {
            collection.AddScoped<IDisplayDriver<TaxCategory>, TaxCategoryDisplayDriver>();
            collection.AddSingleton<INamedCatalogManager<TaxCategory>>(categories);
            collection.AddSingleton<INamedCatalog<TaxRule>>(rules);
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "taxCategory",
            ["ids"] = new[] { "category-1" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var kept = Assert.Single(json.GetProperty("notDeleted").EnumerateArray());

        Assert.Contains("1 tax rule(s)", kept.GetProperty("reason").GetString());
        Assert.Empty(categories.Deleted);
        Assert.Single(categories.Items);
    }

    [Fact]
    public async Task SaveRecord_ChangesOneFieldOfATaxJurisdiction_AndKeepsItsLevelAndDates()
    {
        // Arrange
        var jurisdictions = new InMemoryRecordManager<TaxJurisdiction>();
        jurisdictions.Items.Add(new TaxJurisdiction
        {
            ItemId = "jurisdiction-1",
            Name = "Texas",
            Code = "TX",
            Level = JurisdictionLevel.State,
            Country = "US",
            Region = "TX",
            EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var countries = new Mock<ICountryService>();
        countries.Setup(service => service.GetCountriesAsync()).ReturnsAsync((IReadOnlyList<CountryInfo>)[]);

        using var services = Build(collection =>
        {
            collection.AddScoped<IDisplayDriver<TaxJurisdiction>, TaxJurisdictionDisplayDriver>();
            collection.AddSingleton<INamedCatalogManager<TaxJurisdiction>>(jurisdictions);
            collection.AddSingleton(countries.Object);
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "taxJurisdiction",
            ["id"] = "jurisdiction-1",
            ["values"] = JsonNode.Parse("""{ "City": "Austin", "Name": "Lone Star" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var stored = jurisdictions.Find("jurisdiction-1");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("City", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        Assert.Equal("Name", Assert.Single(json.GetProperty("unchanged").EnumerateArray()).GetString());
        Assert.Equal("Austin", stored.City);
        Assert.Equal("Texas", stored.Name);
        Assert.Equal(JurisdictionLevel.State, stored.Level);
        Assert.Equal("US", stored.Country);
        Assert.Equal(new DateTime(2024, 1, 1), stored.EffectiveFromUtc);
        Assert.Null(stored.EffectiveToUtc);
    }

    private static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddSingleton<INamedCatalog<TaxCategory>>(new Taxation.Fakes.InMemoryNamedCatalog<TaxCategory>());
        services.AddSingleton<INamedCatalog<TaxJurisdiction>>(new Taxation.Fakes.InMemoryNamedCatalog<TaxJurisdiction>());
        services.AddSingleton<INamedCatalog<TaxRule>>(new Taxation.Fakes.InMemoryNamedCatalog<TaxRule>());
        services.AddScoped<AIRecordScopeChanges>();

        new TaxationAIToolsStartup(new RecordTestLocalizer<TaxationAIToolsStartup>()).ConfigureServices(services);

        configure(services);

        return services.BuildServiceProvider();
    }
}
