using System.Diagnostics;

using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoice follows its transaction only after the ledger's change is saved, never by saving the ledger command's
/// session half-way. Each test reads the transaction and the invoice back from a new session.
/// </summary>
public sealed class InvoiceLedgerConsistencyTests
{
    [Fact]
    public async Task APayment_LeavesTheTransactionAndTheInvoiceConsistent()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        var (transaction, stored) = await ReadBothAsync(harness, invoice);
        Assert.Equal((TransactionStatus.Paid, 100m), (transaction.Status, transaction.AmountPaid));
        Assert.Equal((100m, 0m, InvoiceSettlement.Paid), (stored.AmountPaid, stored.AmountDue, stored.Settlement));
    }

    [Fact]
    public async Task ACancelOnTheLedger_IsMirroredAfterItIsSaved()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        var canceled = await harness.OnLedgerAsync(ledger => ledger.CancelAsync(invoice.TransactionId, "Raised in error", TestContext.Current.CancellationToken));

        // Assert: deferred until the cancel was saved, then mirrored.
        Assert.True(canceled.Succeeded, string.Join(" ", canceled.Errors));
        Assert.True(harness.Deferred.Scheduled > 0);
        var (transaction, stored) = await ReadBothAsync(harness, invoice);
        Assert.Equal(TransactionStatus.Canceled, transaction.Status);
        Assert.Equal(0m, stored.AmountDue);
    }

    [Fact]
    public async Task ARefund_IsMirroredAfterItIsSaved()
    {
        // Arrange: paid in full, then 40 refunded through the checkout's refund handler.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken));

        // Act
        await harness.InScopeAsync(async services =>
        {
            var handler = new TransactionRefundHandler(
                services.GetRequiredService<ITransactionManager>(),
                services,
                services.GetRequiredService<IClock>(),
                NullLogger<TransactionRefundHandler>.Instance,
                new Telephony.Doubles.PassThroughStringLocalizer<TransactionRefundHandler>());

            await handler.RefundSucceededAsync(new PaymentRefund
            {
                ItemId = "refund-1",
                ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
                ReferenceId = invoice.TransactionId,
                RefundGrossAmount = 40m,
                Currency = "USD",
            }, TestContext.Current.CancellationToken);

            return true;
        });

        // Assert
        var (transaction, stored) = await ReadBothAsync(harness, invoice);
        Assert.Equal(40m, transaction.AmountRefunded);
        Assert.Equal((60m, InvoiceSettlement.PartiallyPaid), (stored.AmountPaid, stored.Settlement));
        Assert.Contains(stored.History, entry => entry.Kind == DocumentEventKinds.Refunded);
    }

    /// <summary>
    /// Someone else changes the transaction while a payment changes its status, before the payment is saved: the payment
    /// fails as the ledger command's own failure, and neither record claims it was paid.
    /// </summary>
    [Fact]
    public async Task AConcurrentChangeToTheTransaction_FailsTheLedgerCommand_AndIsNotSwallowed()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(writeAheadLog: true);
        var invoice = await IssueAsync(harness);

        harness.OnTransactionStatusChanged = async transaction =>
        {
            harness.OnTransactionStatusChanged = null;

            await harness.InScopeAsync(async services =>
            {
                var transactions = services.GetRequiredService<ITransactionManager>();
                var other = await transactions.FindByIdAsync(transaction.ItemId, TestContext.Current.CancellationToken);

                other.Events.Add(new TransactionEvent { CreatedUtc = InvoicingTestHarness.Now, Type = TransactionEventType.Note, Message = "Changed by someone else." });
                await transactions.UpdateAsync(other, data: null, TestContext.Current.CancellationToken);

                return true;
            });
        };

        // Act
        TransactionCommandResult paid = null;
        Exception failure = null;

        try
        {
            paid = await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken));
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // Assert
        Assert.True(failure is not null || paid is { Succeeded: false }, "The payment must fail when the transaction changed under it.");

        var (transaction, stored) = await ReadBothAsync(harness, invoice);
        Assert.Equal(0m, transaction.AmountPaid);
        Assert.Contains(transaction.Events, entry => entry.Message == "Changed by someone else.");
        Assert.Equal((0m, InvoiceSettlement.Unpaid), (stored.AmountPaid, stored.Settlement));
    }

    [Fact]
    public async Task VoidAndReinstate_DoNotWaitForTheirOwnInvoiceLock()
    {
        // Arrange: the mirror's lock wait is 2 seconds.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var toVoid = await IssueAsync(harness);
        var toWriteOff = await IssueAsync(harness);
        await harness.RunAsync(commands => commands.WriteOffAsync(toWriteOff.ItemId, "Gone", TestContext.Current.CancellationToken));

        // Act
        var watch = Stopwatch.StartNew();
        var voided = await harness.RunAsync(commands => commands.VoidAsync(toVoid.ItemId, "Duplicate", TestContext.Current.CancellationToken));
        var voidTime = watch.Elapsed;

        watch.Restart();
        var reinstated = await harness.RunAsync(commands => commands.ReinstateAsync(toWriteOff.ItemId, "Paid after all", TestContext.Current.CancellationToken));
        var reinstateTime = watch.Elapsed;

        // Assert
        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));
        Assert.True(reinstated.Succeeded, string.Join(" ", reinstated.Errors));
        Assert.True(voidTime < TimeSpan.FromSeconds(1.5), $"The void took {voidTime}.");
        Assert.True(reinstateTime < TimeSpan.FromSeconds(1.5), $"The reinstate took {reinstateTime}.");

        var (_, reopened) = await ReadBothAsync(harness, toWriteOff);
        Assert.Equal((InvoiceStatus.Open, 100m), (reopened.Status, reopened.AmountDue));
    }

    private static async Task<(Transaction Transaction, Invoice Invoice)> ReadBothAsync(InvoicingTestHarness harness, Invoice invoice)
        => (await harness.FindTransactionAsync(invoice.TransactionId), await harness.FindInvoiceAsync(invoice.ItemId));

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
