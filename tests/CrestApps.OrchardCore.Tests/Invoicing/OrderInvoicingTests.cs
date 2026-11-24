using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Orders;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An invoice issued for a debt already on the ledger takes it over instead of posting a second one, follows its
/// payments, and is refused when it doesn't come to what the debt owes; an order whose whole balance is on the ledger is
/// invoiced once from its lines, shipping and discounts, billed to whoever owes the debt.
/// </summary>
public sealed class OrderInvoicingTests
{
    private static readonly PartyRef _account = new(PartyKinds.Account, "acme");

    [Fact]
    public async Task AnInvoiceForADebtOnTheLedger_TakesItOver_AndFollowsItsPayments()
    {
        // Arrange: a $100 debt for an order, and a $100 draft that takes it over.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var debt = await CreateDebtAsync(harness, "order-1", 100m);
        var draft = await harness.CreateDraftAsync(invoice => invoice.TransactionId = debt.ItemId);

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(debt.ItemId, 100m, "check", null, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        Assert.Equal(debt.ItemId, issued.Invoice.TransactionId);

        var transaction = await harness.FindTransactionAsync(debt.ItemId);
        Assert.Contains(transaction.Events, entry => entry.Type == TransactionEventType.Note && entry.DocumentNumber == issued.Invoice.Number);
        Assert.Equal(CheckoutReferenceTypes.Order, transaction.ReferenceType);

        var invoice = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal((InvoiceSettlement.Paid, 0m, 100m), (invoice.Settlement, invoice.AmountDue, invoice.AmountPaid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("INV-0001")]
    public async Task AnInvoiceThatTakesOverADebt_GivesTheDebtItsNumber_SoAReminderDrawsNoOther(string earlierNumber)
    {
        // Arrange: a $100 debt an order left on the ledger (Pay Later, account terms), perhaps numbered by an earlier
        // reminder, and a draft that takes it over.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var debt = await CreateDebtAsync(harness, "order-1", 100m);

        if (earlierNumber is not null)
        {
            await harness.InScopeAsync(async services =>
            {
                var manager = services.GetRequiredService<ITransactionManager>();
                var numbered = await manager.FindByIdAsync(debt.ItemId, TestContext.Current.CancellationToken);

                numbered.InvoiceNumber = earlierNumber;
                await manager.UpdateAsync(numbered, data: null, TestContext.Current.CancellationToken);

                return numbered;
            });
        }

        var draft = await harness.CreateDraftAsync(invoice => invoice.TransactionId = debt.ItemId);

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var transaction = await harness.FindTransactionAsync(debt.ItemId);
        var reminderNumbered = await new TransactionInvoiceService(harness.Numbers).EnsureInvoiceNumberAsync(transaction, TestContext.Current.CancellationToken);

        // Assert: the reminders and the invoice's PDF show one number, and the sequence skips none.
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(issued.Invoice.Number, transaction.InvoiceNumber);
        Assert.False(reminderNumbered);
    }

    [Fact]
    public async Task AnInvoiceThatDoesNotComeToTheDebt_IsNotIssued()
    {
        // Arrange: a $100 debt and a $90 draft.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var debt = await CreateDebtAsync(harness, "order-1", 100m);
        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.TransactionId = debt.ItemId;
            invoice.Lines[0].UnitPrice = 45m;
        });

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(issued.Succeeded);
        Assert.Equal(InvoiceStatus.Draft, (await harness.FindInvoiceAsync(draft.ItemId)).Status);
    }

    [Fact]
    public async Task AnOrderOnAccount_IsInvoicedOnce_FromItsLines()
    {
        // Arrange: 2 widgets at $40, $10 shipping and a $5 discount, $85 on account for Acme.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var order = new Order
        {
            ItemId = "order-7",
            OrderNumber = "1007",
            Currency = "USD",
            Total = 85m,
            CustomerName = "Pat Doe",
            CustomerEmail = "pat@acme.example",
            PurchaseOrderNumber = "PO-55",
            Lines = [new OrderLine { LineId = "l1", Title = "Widget", Sku = "W-1", Quantity = 2, UnitPrice = 40m, LineTotal = 80m }],
            Shipping = new OrderShipping { Name = "Ground", Amount = 10m },
            Discounts = [new OrderDiscount { Code = "SAVE5", Amount = 5m }],
        };
        harness.Orders.Setup(store => store.FindByIdAsync("order-7", It.IsAny<CancellationToken>())).ReturnsAsync(order);
        var debt = await CreateDebtAsync(harness, "order-7", 85m, _account);

        // Act
        var first = await BuildAsync(harness, debt.ItemId);
        var second = await BuildAsync(harness, debt.ItemId);

        // Assert
        Assert.Equal(first.ItemId, second.ItemId);

        var invoice = await harness.FindInvoiceAsync(first.ItemId);
        Assert.Equal((InvoiceStatus.Open, debt.ItemId, InvoicingConstants.OrderReferenceType, "order-7"), (invoice.Status, invoice.TransactionId, invoice.SourceType, invoice.SourceId));
        Assert.Equal(("1007", "PO-55", _account), (invoice.Reference, invoice.PurchaseOrderNumber, invoice.BillTo.Party));
        Assert.Equal([("Widget", 2m, 40m), ("Shipping: Ground", 1m, 10m), ("Discount SAVE5", 1m, -5m)], invoice.Lines.Select(line => (line.Name, line.Quantity, line.UnitPrice)));
        Assert.Equal(85m, invoice.Totals.Total);
    }

    private static Task<Invoice> BuildAsync(InvoicingTestHarness harness, string transactionId)
        => harness.InScopeAsync(services => services.GetRequiredService<OrderInvoiceBuilder>().InvoiceAsync(transactionId, TestContext.Current.CancellationToken));

    private static Task<Transaction> CreateDebtAsync(InvoicingTestHarness harness, string orderId, decimal amount, PartyRef payer = default)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ITransactionManager>();
            var transaction = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            transaction.Title = $"Order {orderId}";
            transaction.Source = "account-terms";
            transaction.OwnerId = "user-1";
            transaction.ReferenceType = CheckoutReferenceTypes.Order;
            transaction.ReferenceId = orderId;
            transaction.Currency = "USD";
            transaction.Amount = amount;
            transaction.TotalAmount = amount;
            transaction.Status = TransactionStatus.Outstanding;
            transaction.CreatedUtc = InvoicingTestHarness.Now;
            transaction.UpdatedUtc = InvoicingTestHarness.Now;

            if (!payer.IsEmpty)
            {
                transaction.SetPayer(payer);
            }

            await manager.CreateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);

            return transaction;
        });
}
