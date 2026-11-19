using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// A completed work order bills its billable lines (time and materials) or one line at its agreed or fixed price, owed by
/// its account, else its customer, else its contact; the billing with the highest priority bills it.
/// </summary>
public sealed class WorkOrderBillingRequestTests
{
    [Fact]
    public void TimeAndMaterials_BillsTheBillableLinesUsed()
    {
        // Arrange
        var workOrder = new WorkOrder
        {
            Number = "WO-1001",
            AccountId = "account-1",
            CustomerId = "user-1",
            Lines =
            [
                new WorkOrderLine { Kind = WorkOrderLineKind.Service, Description = "Labor", QuantityEstimated = 2m, UnitPrice = 80m },
                new WorkOrderLine { Kind = WorkOrderLineKind.Product, Description = "Filter", ProductId = "product-1", QuantityUsed = 3m, UnitPrice = 10m },
                new WorkOrderLine { Kind = WorkOrderLineKind.Product, Description = "Not used", QuantityUsed = 0m, UnitPrice = 99m },
                new WorkOrderLine { Kind = WorkOrderLineKind.Service, Description = "Courtesy check", QuantityUsed = 1m, UnitPrice = 50m, Billable = false },
            ],
        };
        var type = new WorkOrderType { BillingMode = WorkOrderBillingMode.TimeAndMaterials, Currency = "EUR" };

        // Act
        var request = WorkOrderService.CreateBillingRequest(workOrder, type, WorkOrderService.BillableAmount(workOrder, type));

        // Assert
        Assert.Equal((190m, "EUR", new PartyRef(PartyKinds.Account, "account-1")), (request.Amount, request.Currency, request.Payer));
        Assert.Collection(
            request.Lines,
            line => Assert.Equal((WorkOrderLineKind.Service, "Labor", 2m, 80m), (line.Kind, line.Description, line.Quantity, line.UnitPrice)),
            line => Assert.Equal((WorkOrderLineKind.Product, "Filter", 3m, "product-1"), (line.Kind, line.Description, line.Quantity, line.ProductId)));
    }

    [Fact]
    public void AnAgreedPrice_IsBilledAsOneLine_OwedByTheCustomerWithoutAnAccount()
    {
        // Arrange
        var workOrder = new WorkOrder { Number = "WO-1002", Summary = "Boiler service", CustomerId = "user-1", AgreedPrice = 150m };
        var type = new WorkOrderType { BillingMode = WorkOrderBillingMode.TimeAndMaterials };

        // Act
        var request = WorkOrderService.CreateBillingRequest(workOrder, type, WorkOrderService.BillableAmount(workOrder, type));

        // Assert
        var line = Assert.Single(request.Lines);
        Assert.Equal(("Boiler service", 1m, 150m), (line.Description, line.Quantity, line.UnitPrice));
        Assert.Equal(new PartyRef(PartyKinds.User, "user-1"), request.Payer);
        Assert.Equal("USD", request.Currency);
    }

    [Fact]
    public void WithoutAccountOrCustomer_TheContactOwesIt()
        => Assert.Equal(
            new PartyRef(PartyKinds.Contact, "contact-1"),
            WorkOrderService.CreateBillingRequest(new WorkOrder { ContactId = "contact-1" }, new WorkOrderType { BillingMode = WorkOrderBillingMode.FixedPrice, FixedPrice = 90m }, 90m).Payer);

    /// <summary>
    /// A billing that only posts an amount still bills through the request.
    /// </summary>
    [Fact]
    public async Task ABillingThatOnlyPostsAnAmount_BillsTheRequestThroughIt()
    {
        // Arrange
        var billing = new Mock<IWorkOrderBilling> { CallBase = true };
        billing.Setup(value => value.InvoiceAsync(It.IsAny<WorkOrder>(), 190m, "EUR", It.IsAny<CancellationToken>())).ReturnsAsync("transaction-1");

        // Act
        var result = await billing.Object.InvoiceAsync(new WorkOrderBillingRequest { WorkOrder = new WorkOrder(), Amount = 190m, Currency = "EUR" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(("transaction-1", (string)null), (result.TransactionId, result.InvoiceId));
        Assert.Equal(0, billing.Object.Priority);
    }
}
