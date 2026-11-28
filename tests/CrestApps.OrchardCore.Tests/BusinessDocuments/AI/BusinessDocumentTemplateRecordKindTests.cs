using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Drivers;
using CrestApps.OrchardCore.BusinessDocuments.Handlers;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using BusinessDocumentsAIToolsStartup = CrestApps.OrchardCore.BusinessDocuments.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments.AI;

/// <summary>
/// The business document templates are a read-write record kind with the Document templates permission, written through
/// that page's editor and handler. A template saved as the default takes the default from the other templates of its
/// kinds, as on the page, a document kind no feature registers is refused, and the footer is cut in what the tools show.
/// </summary>
public sealed class BusinessDocumentTemplateRecordKindTests
{
    [Fact]
    public void TheKind_IsReadWrite_WithTheTemplatesPermission_ForTheFeature_WhenTheAIFeatureIsOn()
    {
        // Arrange
        using var services = Build(new InMemoryBusinessDocumentTemplateStore());

        // Act
        var kind = RecordToolHarness.Kinds(services).Value.Kinds[BusinessDocumentsAIToolsStartup.TemplateKind];
        var required = typeof(BusinessDocumentsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(BusinessDocumentsAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.True(kind.CanWrite);
        Assert.Equal(BusinessDocumentsPermissions.ManageBusinessDocumentTemplates.Name, kind.Permission.Name);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(BusinessDocumentsConstants.Features.Area, feature.FeatureName);
    }

    [Fact]
    public async Task SaveRecord_ANewDefaultTemplate_TakesTheDefaultFromTheOthersOfItsKind()
    {
        // Arrange
        var store = Store();
        using var services = Build(store);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = BusinessDocumentsAIToolsStartup.TemplateKind,
            ["values"] = JsonNode.Parse("""{ "Name": "Invoices 2027", "Kinds": ["Invoice"], "IsDefault": true, "UseAccentColor": true, "AccentColor": "#16a34a" }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = store.Entries.Single(template => template.Name == "Invoices 2027");

        Assert.True(json.GetProperty("saved").GetBoolean(), result);
        Assert.True(created.IsDefault);
        Assert.Equal("#16A34A", created.AccentColor);
        Assert.False(store.Entries.Single(template => template.ItemId == "invoice").IsDefault);
        Assert.True(store.Entries.Single(template => template.ItemId == "every").IsDefault);
    }

    [Fact]
    public async Task SaveRecord_ADocumentKindNoFeatureRegisters_IsRefused()
    {
        // Arrange
        var store = Store();
        using var services = Build(store);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = BusinessDocumentsAIToolsStartup.TemplateKind,
            ["values"] = JsonNode.Parse("""{ "Name": "Quotes", "Kinds": ["Quote"] }"""),
        });

        // Assert
        Assert.Contains("These are not document kinds: Quote", result);
        Assert.DoesNotContain(store.Entries, template => template.Name == "Quotes");
    }

    [Fact]
    public async Task SaveRecord_WithoutTheTemplatesPermission_SavesNothing()
    {
        // Arrange
        var store = Store();
        using var services = Build(store, (permission, _) => permission.Name != BusinessDocumentsPermissions.ManageBusinessDocumentTemplates.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = BusinessDocumentsAIToolsStartup.TemplateKind,
            ["values"] = JsonNode.Parse("""{ "Name": "Receipts" }"""),
        });

        // Assert
        Assert.DoesNotContain("\"saved\":true", result);
        Assert.Equal(2, store.Entries.Count);
    }

    [Fact]
    public async Task TheDefaultHandOff_TheTemplatesPageUses_KeepsTheDefaultOfOtherKinds()
    {
        // Arrange
        var store = Store();
        var wholesale = new BusinessDocumentTemplate { ItemId = "wholesale", Name = "Wholesale", Kinds = ["Invoice", "Estimate"], IsDefault = true };
        store.Entries.Add(wholesale);

        // Act
        var replaced = await BusinessDocumentTemplateDefaults.TakeOverDefaultAsync(store.CreateManager(), wholesale, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("invoice", Assert.Single(replaced).ItemId);
        Assert.True(store.Entries.Single(template => template.ItemId == "every").IsDefault);
    }

    [Fact]
    public void TheProjection_CutsTheFooter_AndNeverShowsTheProperties()
    {
        // Arrange
        var template = new BusinessDocumentTemplate { ItemId = "t1", Name = "Long", Footer = new string('x', 600) };
        template.Properties["Branding"] = "CANARY-properties";

        // Act
        var json = JsonSerializer.SerializeToElement(BusinessDocumentsAIToolsStartup.DescribeTemplate(template));

        // Assert
        Assert.DoesNotContain("CANARY", json.GetRawText());
        Assert.Equal(500, json.GetProperty("footer").GetString().Length);
        Assert.True(json.GetProperty("footerTruncated").GetBoolean());
    }

    // A default template for every kind and one for invoices.
    private static InMemoryBusinessDocumentTemplateStore Store()
    {
        var store = new InMemoryBusinessDocumentTemplateStore();

        store.Entries.Add(new BusinessDocumentTemplate { ItemId = "every", Name = "Every kind", IsDefault = true });
        store.Entries.Add(new BusinessDocumentTemplate { ItemId = "invoice", Name = "Invoices", IsDefault = true, Kinds = ["Invoice"] });

        return store;
    }

    private static ServiceProvider Build(InMemoryBusinessDocumentTemplateStore store, Func<global::OrchardCore.Security.Permissions.Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(authorize: authorize);
        var handler = new BusinessDocumentTemplateHandler(new HttpContextAccessor(), new TestClock(DateTime.UtcNow), new RecordTestLocalizer<BusinessDocumentTemplateHandler>());

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<AIRecordScopeChanges>();
        services.AddScoped<IDisplayDriver<BusinessDocumentTemplate>, BusinessDocumentTemplateDisplayDriver>();
        services.AddSingleton<IBusinessDocumentTemplateManager>(store.CreateManager(handler));
        services.Configure<BusinessDocumentOptions>(options =>
        {
            options.Kinds["Invoice"] = new BusinessDocumentKind { Name = "Invoice" };
            options.Kinds["Receipt"] = new BusinessDocumentKind { Name = "Receipt" };
        });

        new BusinessDocumentsAIToolsStartup(new RecordTestLocalizer<BusinessDocumentsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }
}
