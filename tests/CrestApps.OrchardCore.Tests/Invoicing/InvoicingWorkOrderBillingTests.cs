using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// With Invoicing on, a completed work order is billed as an issued invoice with its labor and parts, billed to whoever
/// owes it, once.
/// </summary>
public sealed class InvoicingWorkOrderBillingTests
{
    [Fact]
    public async Task AWorkOrder_IsBilledAsAnIssuedInvoiceWithItsLines_Once()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var request = new WorkOrderBillingRequest
        {
            WorkOrder = new WorkOrder
            {
                ItemId = "work-order-1",
                Number = "WO-1001",
                Summary = "Boiler repair",
                AccountId = "account-1",
                Contact = new WorkOrderContact { Name = "Pat Doe", Email = "pat@fabrikam.example" },
            },
            Amount = 190m,
            Currency = "USD",
            Payer = new PartyRef(PartyKinds.Account, "account-1"),
            TaxCategoryCode = "services",
            Lines =
            [
                new WorkOrderBillingLine { Kind = WorkOrderLineKind.Service, Description = "Labor", Quantity = 2m, UnitPrice = 80m },
                new WorkOrderBillingLine { Kind = WorkOrderLineKind.Product, Description = "Filter", Quantity = 3m, UnitPrice = 10m, ProductId = "product-1", TaxCategoryCode = "goods" },
            ],
        };

        // Act
        var first = await BillAsync(harness, request);
        var again = await BillAsync(harness, request);

        // Assert
        Assert.False(string.IsNullOrEmpty(first.TransactionId));
        Assert.Equal((first.InvoiceId, first.TransactionId), (again.InvoiceId, again.TransactionId));
        Assert.Equal("INV-1001", first.InvoiceNumber);

        var invoice = await harness.FindInvoiceAsync(first.InvoiceId);
        Assert.Equal((InvoiceStatus.Open, WorkOrdersConstants.ReferenceType, "work-order-1", "WO-1001"), (invoice.Status, invoice.SourceType, invoice.SourceId, invoice.Reference));
        Assert.Equal(new PartyRef(PartyKinds.Account, "account-1"), invoice.BillTo.Party);
        Assert.Equal("pat@fabrikam.example", invoice.BillTo.Contact.Email);
        Assert.Collection(
            invoice.Lines,
            line => Assert.Equal((DocumentLineKind.Service, "Labor", 160m, "services"), (line.Kind, line.Name, line.Net, line.TaxCategoryCode)),
            line => Assert.Equal((DocumentLineKind.Item, "Filter", 30m, "goods", "product-1"), (line.Kind, line.Name, line.Net, line.TaxCategoryCode, line.ProductId)));
        Assert.Equal(190m, invoice.Totals.Total);

        var transaction = await harness.FindTransactionAsync(first.TransactionId);
        Assert.Equal(190m, transaction.TotalAmount);
    }

    [Fact]
    public void InvoicingBilling_WinsOverThePlainDebt()
        => Assert.True(new InvoicingWorkOrderBilling(null, null, NullLogger<InvoicingWorkOrderBilling>.Instance).Priority > 0);

    private static Task<WorkOrderBillingResult> BillAsync(InvoicingTestHarness harness, WorkOrderBillingRequest request)
        => harness.InScopeAsync(services => new InvoicingWorkOrderBilling(
            services.GetRequiredService<IInvoiceManager>(),
            services.GetRequiredService<IInvoiceCommandService>(),
            NullLogger<InvoicingWorkOrderBilling>.Instance).InvoiceAsync(request, TestContext.Current.CancellationToken));
}
