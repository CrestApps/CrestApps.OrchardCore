using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// The Invoicing record kinds: estimates and recurring plans are read-only and read with <c>View invoices</c>, usage
/// meters and billable charges are written through their editors with <c>Manage invoices</c>. The lists take a status
/// name as their filter, the projections are allow-lists, and a charge an invoice bills is never deleted.
/// </summary>
public sealed class InvoicingCreditRecordKindTests
{
    [Theory]
    [InlineData(InvoicingRecordKinds.EstimateKind, false, "ManageEstimates", "ViewInvoices")]
    [InlineData(InvoicingRecordKinds.RecurringPlanKind, false, "ManageRecurringInvoices", "ViewInvoices")]
    [InlineData(InvoicingRecordKinds.UsageMeterKind, true, "ManageInvoices", null)]
    [InlineData(InvoicingRecordKinds.BillableItemKind, true, "ManageInvoices", null)]
    public void Kind_IsRegisteredWithItsPermissionsAndWriteAccess(string name, bool canWrite, string permission, string readPermission)
    {
        // Act
        var kinds = GetKinds();

        // Assert
        Assert.True(kinds.TryGetValue(name, out var kind), $"The record kind '{name}' is not registered.");
        Assert.Equal(canWrite, kind.CanWrite);
        Assert.Equal(permission, kind.Permission.Name);
        Assert.Equal(readPermission, kind.ReadPermission?.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Title));
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Fact]
    public async Task EstimateKind_ListsByStatusName_AndReturnsOnlyItsAllowList()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await InvoicingCreditToolContext.CreateEstimateAsync(harness, estimate => estimate.InternalNote = "Margin is thin, do not discount");
        var sent = await InvoicingCreditToolContext.CreateEstimateAsync(harness);
        await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().SendAsync(sent.ItemId, TestContext.Current.CancellationToken));
        var kind = Assert.IsType<AIRecordKind<Estimate>>(GetKinds()[InvoicingRecordKinds.EstimateKind]);

        // Act
        var sentPage = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "sent" }, TestContext.Current.CancellationToken).AsTask());
        var draftPage = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "Draft" }, TestContext.Current.CancellationToken).AsTask());

        // Assert
        Assert.Equal(sent.ItemId, Assert.Single(sentPage.Entries).ItemId);
        var draft = Assert.Single(draftPage.Entries);
        var projected = JsonSerializer.Serialize(kind.Projection(draft));
        Assert.Contains("\"lines\"", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin is thin", projected, StringComparison.Ordinal);
        Assert.DoesNotContain("History", projected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecurringPlanKind_ListsByStatusName()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IRecurringPlanManager>();
            var plan = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            plan.Name = "Hosting";
            plan.Rule = "FREQ=MONTHLY";
            plan.StartDate = DateOnly.FromDateTime(InvoicingTestHarness.Now);
            plan.Currency = "USD";
            plan.Lines.Add(new DocumentLine { Id = "line-1", Name = "Hosting", Quantity = 1m, UnitPrice = 100m });

            await manager.CreateAsync(plan, TestContext.Current.CancellationToken);

            return plan;
        });
        var kind = Assert.IsType<AIRecordKind<RecurringPlan>>(GetKinds()[InvoicingRecordKinds.RecurringPlanKind]);

        // Act
        var drafts = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "draft" }, TestContext.Current.CancellationToken).AsTask());
        var active = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "active" }, TestContext.Current.CancellationToken).AsTask());

        // Assert
        var plan = Assert.Single(drafts.Entries);
        Assert.Empty(active.Entries);
        Assert.Contains("\"mode\":\"draft\"", JsonSerializer.Serialize(kind.Projection(plan)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BillableItemKind_ListsUnbilledCharges_AndKeepsABilledOne()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (unbilled, billed) = await harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IBillableItemStore>();
            var open = Charge("Travel to site", null);
            var invoiced = Charge("Hotel", "invoice-1");

            await store.CreateAsync(open, TestContext.Current.CancellationToken);
            await store.CreateAsync(invoiced, TestContext.Current.CancellationToken);

            return (open, invoiced);
        });
        var kind = Assert.IsType<AIRecordKind<BillableItem>>(GetKinds()[InvoicingRecordKinds.BillableItemKind]);

        // Act
        var page = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "unbilled" }, TestContext.Current.CancellationToken).AsTask());
        var search = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "Hotel" }, TestContext.Current.CancellationToken).AsTask());
        var keptBilled = await kind.InUseAsync(null, billed, TestContext.Current.CancellationToken);
        var keptUnbilled = await kind.InUseAsync(null, unbilled, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(unbilled.ItemId, Assert.Single(page.Entries).ItemId);
        Assert.Equal(billed.ItemId, Assert.Single(search.Entries).ItemId);
        Assert.Contains("Take it off the invoice first", keptBilled, StringComparison.Ordinal);
        Assert.Null(keptUnbilled);
    }

    [Fact]
    public async Task UsageMeterKind_ListsByNameOrCode()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<IUsageMeterStore>();

            await store.CreateAsync(new UsageMeter { ItemId = "meter-1", Name = "API calls", Code = "api-calls" }, TestContext.Current.CancellationToken);
            await store.CreateAsync(new UsageMeter { ItemId = "meter-2", Name = "Storage", Code = "storage-gb", Aggregation = UsageAggregation.Maximum }, TestContext.Current.CancellationToken);

            return true;
        });
        var kind = Assert.IsType<AIRecordKind<UsageMeter>>(GetKinds()[InvoicingRecordKinds.UsageMeterKind]);

        // Act
        var page = await harness.InScopeAsync(services => kind.ListAsync(services, new AIRecordQuery { Text = "storage-gb" }, TestContext.Current.CancellationToken).AsTask());

        // Assert
        var meter = Assert.Single(page.Entries);
        Assert.Contains("\"aggregation\":\"maximum\"", JsonSerializer.Serialize(kind.Projection(meter)), StringComparison.Ordinal);
    }

    private static BillableItem Charge(string description, string invoiceId)
        => new()
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Party = new PartyRef("Account", "7"),
            PartyName = "Fabrikam Ltd.",
            Date = DateOnly.FromDateTime(InvoicingTestHarness.Now),
            Description = description,
            Quantity = 1m,
            UnitCost = 80m,
            Currency = "USD",
            InvoiceId = invoiceId,
        };

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds()
    {
        var services = new ServiceCollection();

        new EstimateAIToolsStartup(new PassThroughStringLocalizer<EstimateAIToolsStartup>()).ConfigureServices(services);
        new RecurringPlanAIToolsStartup(new PassThroughStringLocalizer<RecurringPlanAIToolsStartup>()).ConfigureServices(services);
        new BillingAIToolsStartup(new PassThroughStringLocalizer<BillingAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }
}
