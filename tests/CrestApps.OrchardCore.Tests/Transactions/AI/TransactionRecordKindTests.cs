using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.AI;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Drivers;
using CrestApps.OrchardCore.Transactions.Handlers;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Transactions.AI;

/// <summary>
/// Transactions gives the generic record tools two kinds: <c>paymentTerm</c>, written through the Payment terms page's
/// own driver and rules under Manage payment terms, and <c>partyCollectionSettings</c>, read-only because its card posts
/// to a controller, which lists only the parties kept out of reminders or late fees and sends a save to
/// <c>setPartyCollectionSettings</c>. Both return an allow-list of fields.
/// </summary>
public sealed class TransactionRecordKindTests
{
    private readonly List<PaymentTerm> _created = [];

    [Fact]
    public void ThePaymentTermsStartup_RegistersAWritablePaymentTermKind_WithManagePaymentTerms()
    {
        // Act
        var kind = GetKinds(new PaymentTermsAIToolsStartup(new PassThroughStringLocalizer<PaymentTermsAIToolsStartup>()))[TransactionRecordKinds.PaymentTermKind];

        // Assert
        Assert.Equal(typeof(PaymentTerm), kind.RecordType);
        Assert.Equal(TransactionsPermissions.ManagePaymentTerms.Name, kind.Permission.Name);
        Assert.Equal(TransactionsPermissions.ManagePaymentTerms.Name, kind.EffectiveReadPermission.Name);
        Assert.True(kind.CanWrite);
        Assert.NotEqual(kind.Name, kind.Title);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Fact]
    public void ThePaymentTermProjection_IsAnAllowList()
    {
        // Arrange
        var term = new PaymentTerm { ItemId = "net-30", Name = "Net 30", Kind = PaymentTermKind.NetDays, Days = 30, DiscountPercent = 2m, DiscountDays = 10, OwnerId = "user-1", Author = "pat" };

        // Act
        using var projected = JsonDocument.Parse(JsonSerializer.Serialize(TransactionRecordKinds.ProjectPaymentTerm(term)));

        // Assert
        Assert.Equal(
            new[] { "days", "dayOfMonth", "description", "discountDays", "discountPercent", "enabled", "id", "kind", "name" }.Order(StringComparer.Ordinal),
            projected.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("NetDays", projected.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task SaveRecord_CreatesAPaymentTerm_ThroughThePaymentTermEditor()
    {
        // Arrange
        using var services = BuildPaymentTerms();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TransactionRecordKinds.PaymentTermKind,
            ["values"] = JsonNode.Parse("""{ "Name": " Net 30 ", "Kind": "NetDays", "Days": 30, "DayOfMonth": 15, "Enabled": true }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var created = Assert.Single(_created);

        Assert.True(json.GetProperty("saved").GetBoolean(), result);
        Assert.Equal("Net 30", created.Name);
        Assert.Equal(PaymentTermKind.NetDays, created.Kind);
        Assert.Equal(30, created.Days);

        // The driver keeps the day of the month only for a term due on a day of the next month.
        Assert.Equal(0, created.DayOfMonth);
    }

    [Fact]
    public async Task SaveRecord_RefusesADayOfTheMonthThePageRefuses()
    {
        // Arrange
        using var services = BuildPaymentTerms();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TransactionRecordKinds.PaymentTermKind,
            ["values"] = JsonNode.Parse("""{ "Name": "Day 40", "Kind": "DayOfNextMonth", "DayOfMonth": 40 }"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains("from 1 to 31", result, StringComparison.Ordinal);
        Assert.Empty(_created);
    }

    [Fact]
    public void TheActionsStartup_RegistersAReadOnlyCollectionSettingsKind_SavedWithItsTool()
    {
        // Act
        var kind = GetKinds(new TransactionActionsAIToolsStartup(new PassThroughStringLocalizer<TransactionActionsAIToolsStartup>()))[TransactionRecordKinds.PartyCollectionSettingsKind];

        // Assert
        Assert.Equal(TransactionsPermissions.ManageTransactions.Name, kind.Permission.Name);
        Assert.False(kind.CanWrite);
        Assert.Equal(SetPartyCollectionSettingsTool.TheName, kind.SaveToolName);
    }

    [Fact]
    public async Task SaveRecord_SendsACollectionSettingsSave_ToItsTool()
    {
        // Arrange
        var collection = AgentToolTestContext.CreateServices();
        collection.AddScoped<AIRecordScopeChanges>();
        new TransactionActionsAIToolsStartup(new PassThroughStringLocalizer<TransactionActionsAIToolsStartup>()).ConfigureServices(collection);
        using var services = collection.BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TransactionRecordKinds.PartyCollectionSettingsKind,
            ["values"] = JsonNode.Parse("""{ "RemindersPaused": true }"""),
        });

        // Assert
        Assert.Contains(SetPartyCollectionSettingsTool.TheName, result, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCollectionSettingsList_MatchesAPartysIdOrKind_AndPages()
    {
        // Arrange
        var settings = new[]
        {
            new PartyCollectionSettings { ItemId = "s1", Party = new PartyRef(PartyKinds.Customer, "customer-2"), LateFeesExempt = true },
            new PartyCollectionSettings { ItemId = "s2", Party = new PartyRef(PartyKinds.Account, "account-7"), RemindersPaused = true },
            new PartyCollectionSettings { ItemId = "s3", Party = new PartyRef(PartyKinds.Account, "account-9"), RemindersPaused = true },
        };

        // Act
        var accounts = TransactionRecordKinds.Page(settings, new AIRecordQuery { Text = "account" });
        var one = TransactionRecordKinds.Page(settings, new AIRecordQuery { Text = "Account:account-7" });
        var firstPage = TransactionRecordKinds.Page(settings, new AIRecordQuery { PageSize = 2 });

        // Assert
        Assert.Equal(["s2", "s3"], accounts.Entries.Select(item => item.ItemId));
        Assert.Equal("s2", Assert.Single(one.Entries).ItemId);
        Assert.Equal(3, firstPage.Count);
        Assert.Equal(["s2", "s3"], firstPage.Entries.Select(item => item.ItemId));
    }

    [Fact]
    public void TheCollectionSettingsProjection_IsAnAllowList()
    {
        // Arrange
        var settings = new PartyCollectionSettings { ItemId = "s2", Party = new PartyRef(PartyKinds.Account, "account-7"), RemindersPaused = true, ModifiedUtc = TransactionActionToolContext.Now };

        // Act
        using var projected = JsonDocument.Parse(JsonSerializer.Serialize(TransactionRecordKinds.ProjectPartyCollectionSettings(settings)));

        // Assert
        Assert.Equal(
            new[] { "id", "lateFeesExempt", "party", "partyId", "partyKind", "remindersPaused" }.Order(StringComparer.Ordinal),
            projected.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Account:account-7", projected.RootElement.GetProperty("party").GetString());
    }

    private ServiceProvider BuildPaymentTerms()
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<AIRecordScopeChanges>();
        services.AddScoped<IDisplayDriver<PaymentTerm>, PaymentTermDisplayDriver>();

        // The page's rules run where the catalog manager validates: PaymentTermHandler.
        var handler = new PaymentTermHandler(Mock.Of<IHttpContextAccessor>(), Mock.Of<IClock>(), new PassThroughStringLocalizer<PaymentTermHandler>());
        var manager = new Mock<IPaymentTermManager>();
        manager.Setup(terms => terms.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new PaymentTerm());
        manager.Setup(terms => terms.ValidateAsync(It.IsAny<PaymentTerm>(), It.IsAny<CancellationToken>()))
            .Returns((PaymentTerm term, CancellationToken cancellationToken) => new ValueTask<ValidationResultDetails>(ValidateAsync(handler, term, cancellationToken)));
        manager.Setup(terms => terms.CreateAsync(It.IsAny<PaymentTerm>(), It.IsAny<CancellationToken>()))
            .Callback((PaymentTerm term, CancellationToken _) => _created.Add(term))
            .Returns(ValueTask.CompletedTask);
        services.AddSingleton(manager.Object);

        new PaymentTermsAIToolsStartup(new PassThroughStringLocalizer<PaymentTermsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private static async Task<ValidationResultDetails> ValidateAsync(PaymentTermHandler handler, PaymentTerm term, CancellationToken cancellationToken)
    {
        var context = new ValidatingContext<PaymentTerm>(term);

        await handler.ValidatingAsync(context, cancellationToken);

        return context.Result;
    }

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(StartupBase startup)
    {
        var services = new ServiceCollection();

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }
}
