using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Workflows;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// The work order integrations: parts come off Inventory in whole units with the work order as the reference, a
/// completed work order becomes an outstanding invoice that refers to it, a service address gets its territory and is
/// geocoded, and the workflow events get the work order's details.
/// </summary>
public sealed class WorkOrderIntegrationTests
{
    [Fact]
    public async Task Parts_ComeOffTheirLocation_WithTheWorkOrderAsTheReference()
    {
        var inventory = new Mock<IInventoryService>();

        inventory.Setup(service => service.ConsumeAsync("stock-1", "van-1", 2, WorkOrdersConstants.ReferenceType, "wo-1", "WO-00001", It.IsAny<StockActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockChangeResult { Succeeded = true });

        var parts = new InventoryWorkOrderParts(inventory.Object, new NullStringLocalizer<InventoryWorkOrderParts>());
        var refused = await parts.UseAsync(new WorkOrder { ItemId = "wo-1", Number = "WO-00001" }, new WorkOrderLine { StockItemId = "stock-1", LocationId = "van-1", Description = "Filter" }, 2m, "tech", TestContext.Current.CancellationToken);

        Assert.Null(refused);
        inventory.VerifyAll();
    }

    [Fact]
    public async Task Parts_AreRefused_InFractions_OrWhenTheLocationIsShort()
    {
        var inventory = new Mock<IInventoryService>();

        inventory.Setup(service => service.ConsumeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StockActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockChangeResult { Error = StockChangeError.NotEnoughAtLocation });

        var parts = new InventoryWorkOrderParts(inventory.Object, new NullStringLocalizer<InventoryWorkOrderParts>());
        var line = new WorkOrderLine { StockItemId = "stock-1", LocationId = "van-1", Description = "Filter" };

        Assert.NotNull(await parts.UseAsync(new WorkOrder(), line, 1.5m, "tech", TestContext.Current.CancellationToken));
        inventory.Verify(service => service.ConsumeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StockActor>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.NotNull(await parts.UseAsync(new WorkOrder(), line, 3m, "tech", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACompletedWorkOrder_BecomesAnOutstandingInvoice_ThatRefersToIt()
    {
        var created = new Transaction();
        var transactions = new Mock<ITransactionManager>();
        var invoices = new Mock<ITransactionInvoiceService>();
        var clock = new Mock<IClock>();

        clock.Setup(item => item.UtcNow).Returns(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
        transactions.Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() =>
        {
            created.ItemId = "tx-1";

            return created;
        });
        invoices.Setup(service => service.EnsureInvoiceNumberAsync(created, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var billing = new TransactionsWorkOrderBilling(transactions.Object, invoices.Object, clock.Object, new NullStringLocalizer<TransactionsWorkOrderBilling>());
        var workOrder = new WorkOrder { ItemId = "wo-1", Number = "WO-00001", Summary = "No heat", CustomerId = "customer-1", Contact = new WorkOrderContact { Name = "Pat", Email = "pat@example.com" } };

        var id = await billing.InvoiceAsync(workOrder, 240m, "USD", TestContext.Current.CancellationToken);

        Assert.Equal("tx-1", id);
        Assert.Equal(WorkOrdersConstants.ReferenceType, created.ReferenceType);
        Assert.Equal("wo-1", created.ReferenceId);
        Assert.Equal("customer-1", created.OwnerId);
        Assert.Equal(TransactionStatus.Outstanding, created.Status);
        Assert.Equal(240m, created.TotalAmount);
        Assert.Equal("USD", created.Currency);
        Assert.Single(created.Events, item => item.Type == TransactionEventType.Created);
        invoices.VerifyAll();
        transactions.Verify(manager => manager.CreateAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AServiceAddress_GetsItsTerritory_AndIsGeocoded_WhenItHasNoPoint()
    {
        var territory = new ServiceTerritory { ItemId = "north", Name = "North" };
        var territories = new Mock<ITerritoryResolver>();
        var queue = new Mock<IGeocodeQueue>();

        territories.Setup(resolver => resolver.ResolveAsync(null, "10001", It.IsAny<CancellationToken>())).ReturnsAsync(new TerritoryMatch(territory, TerritoryMatchKind.PostalCode));

        var handler = new GeoWorkOrderHandler(territories.Object, new Lazy<IGeocodeQueue>(() => queue.Object));
        var workOrder = new WorkOrder { ItemId = "wo-1", ServiceAddress = new Address { AddressLine1 = "1 Main St", City = "New York", PostalCode = "10001" } };

        await handler.CreatingAsync(workOrder, TestContext.Current.CancellationToken);

        Assert.Equal("north", workOrder.TerritoryId);
        queue.Verify(item => item.EnqueueAsync(GeoWorkOrderHandler.TargetType, "wo-1", It.Is<GeocodeQuery>(query => query.Address == workOrder.ServiceAddress), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnUnchangedAddress_IsNotGeocodedAgain_WhenTheWorkOrderIsEdited()
    {
        var territories = new Mock<ITerritoryResolver>();
        var queue = new Mock<IGeocodeQueue>();
        var handler = new GeoWorkOrderHandler(territories.Object, new Lazy<IGeocodeQueue>(() => queue.Object));
        var workOrder = new WorkOrder { ItemId = "wo-1", TerritoryId = "north", ServiceAddress = new Address { AddressLine1 = "1 Main St", PostalCode = "10001", Latitude = 40.7, Longitude = -74 } };

        await handler.UpdatingAsync(workOrder, TestContext.Current.CancellationToken);

        Assert.Equal("north", workOrder.TerritoryId);
        territories.VerifyNoOtherCalls();
        queue.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheWorkflowEvents_GetTheWorkOrdersDetails()
    {
        var workflows = new Mock<IWorkflowManager>();
        IDictionary<string, object> input = null;

        workflows.Setup(manager => manager.TriggerEventAsync(WorkOrdersConstants.Events.StatusChanged, It.IsAny<IDictionary<string, object>>(), "wo-1", false, false))
            .Callback((string _, IDictionary<string, object> value, string _, bool _, bool _) => input = value)
            .ReturnsAsync([]);

        var observer = new WorkflowWorkOrderObserver(workflows.Object);
        var workOrder = new WorkOrder { ItemId = "wo-1", Number = "WO-00001", TypeId = "repair", Status = WorkOrderStatus.InProgress, Priority = WorkOrderPriority.Urgent };

        await observer.OnEventAsync(workOrder, WorkOrdersConstants.Events.StatusChanged, new Dictionary<string, object> { ["PreviousStatus"] = "Scheduled" }, TestContext.Current.CancellationToken);

        Assert.NotNull(input);
        Assert.Equal("WO-00001", input["Number"]);
        Assert.Equal("repair", input["TypeId"]);
        Assert.Equal("InProgress", input["Status"]);
        Assert.Equal("Urgent", input["Priority"]);
        Assert.Equal("Scheduled", input["PreviousStatus"]);
    }

    private sealed class NullStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
