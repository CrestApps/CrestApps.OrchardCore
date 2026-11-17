using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Orders;
using CrestApps.OrchardCore.Invoicing.Renewals;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A renewal cycle's debt, written on the ledger by the provider that bills the agreement (Pay Later for a subscription
/// with no gateway), gets an issued invoice that takes the debt over, once; the first cycle is left to its order, and
/// the order's invoice is never made from a renewal's debt.
/// </summary>
public sealed class RenewalInvoicingTests
{
    private static readonly DateTime _periodStart = new(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ARenewalsDebt_IsInvoicedOnce_TakingTheDebtOver()
    {
        // Arrange: cycle 2 of a $20 monthly membership, owed on the ledger.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var debt = await CreateCycleAsync(harness, cycle: 2, amount: 20m);

        // Act
        var first = await BuildAsync(harness, debt.ItemId);
        var second = await BuildAsync(harness, debt.ItemId);

        // Assert
        Assert.NotNull(first);
        Assert.Equal(first.ItemId, second.ItemId);

        var invoice = await harness.FindInvoiceAsync(first.ItemId);
        Assert.Equal((InvoiceStatus.Open, debt.ItemId, InvoicingConstants.RenewalReferenceType, debt.ItemId), (invoice.Status, invoice.TransactionId, invoice.SourceType, invoice.SourceId));
        Assert.Equal(20m, invoice.Totals.Total);

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(("Membership", 1m, 20m), (line.Name, line.Quantity, line.UnitPrice));
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), (line.ServiceStart.Value, line.ServiceEnd.Value));

        var transaction = await harness.FindTransactionAsync(debt.ItemId);
        Assert.Contains(transaction.Events, entry => entry.Type == TransactionEventType.Note && entry.DocumentNumber == invoice.Number);
    }

    [Fact]
    public async Task TheFirstCycle_OrADebtThatDoesNotRecur_IsNotARenewal()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var firstCycle = await CreateCycleAsync(harness, cycle: 1, amount: 20m);
        var oneOff = await CreateCycleAsync(harness, cycle: 0, amount: 20m);

        // Act
        var forFirst = await BuildAsync(harness, firstCycle.ItemId);
        var forOneOff = await BuildAsync(harness, oneOff.ItemId);

        // Assert
        Assert.Null(forFirst);
        Assert.Null(forOneOff);
    }

    [Fact]
    public async Task ARenewalOfAnOrdersSubscription_IsNotInvoicedAsTheOrder()
    {
        // Arrange: an order of $20 whose subscription renews for $20, the renewal's debt still pointing at the order.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        harness.Orders.Setup(store => store.FindByIdAsync("order-9", It.IsAny<CancellationToken>())).ReturnsAsync(new Order
        {
            ItemId = "order-9",
            OrderNumber = "1009",
            Currency = "USD",
            Total = 20m,
            Lines = [new OrderLine { LineId = "l1", Title = "Membership", Quantity = 1, UnitPrice = 20m, LineTotal = 20m }],
        });
        var debt = await CreateCycleAsync(harness, cycle: 2, amount: 20m, orderId: "order-9");

        // Act
        var asOrder = await harness.InScopeAsync(services => services.GetRequiredService<OrderInvoiceBuilder>().InvoiceAsync(debt.ItemId, TestContext.Current.CancellationToken));
        var asRenewal = await BuildAsync(harness, debt.ItemId);

        // Assert
        Assert.Null(asOrder);
        Assert.Equal(InvoicingConstants.RenewalReferenceType, asRenewal.SourceType);
    }

    private static Task<Invoice> BuildAsync(InvoicingTestHarness harness, string transactionId)
        => harness.InScopeAsync(services => services.GetRequiredService<RenewalInvoiceBuilder>().InvoiceAsync(transactionId, TestContext.Current.CancellationToken));

    private static Task<Transaction> CreateCycleAsync(InvoicingTestHarness harness, int cycle, decimal amount, string orderId = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<ITransactionManager>();
            var transaction = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            transaction.Title = "Membership";
            transaction.Source = "PayLater";
            transaction.OwnerId = "user-1";
            transaction.GuestContactName = "Pat Doe";
            transaction.GuestContactEmail = "pat@example.com";
            transaction.ReferenceType = orderId is null ? null : CheckoutReferenceTypes.Order;
            transaction.ReferenceId = orderId;
            transaction.ObligationId = "obligation-1:cycle-" + cycle;
            transaction.Currency = "USD";
            transaction.Amount = amount;
            transaction.TotalAmount = amount;
            transaction.Status = TransactionStatus.Outstanding;
            transaction.CreatedUtc = InvoicingTestHarness.Now;
            transaction.UpdatedUtc = InvoicingTestHarness.Now;
            transaction.DueUtc = _periodStart.AddDays(14);

            if (cycle > 0)
            {
                transaction.Recurrence = new TransactionRecurrence
                {
                    BillingDuration = 1,
                    DurationType = DurationType.Month,
                    PeriodStartUtc = _periodStart,
                    PeriodEndUtc = _periodStart.AddMonths(1),
                    CycleNumber = cycle,
                    CycleAmount = amount,
                };
            }

            await manager.CreateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);

            return transaction;
        });
}
