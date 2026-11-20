using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An issued invoice follows its transaction: what was paid, what is still due, whether it is settled, and a history entry
/// per payment and payment taken back. The ledger stays the record.
/// </summary>
public sealed class InvoiceLedgerSyncTests
{
    [Fact]
    public async Task PaymentsOnTheLedger_ShowOnTheInvoice_UntilItIsPaid()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        // Act
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 30m }, TestContext.Current.CancellationToken));
        var partly = await harness.FindInvoiceAsync(invoice.ItemId);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 70m }, TestContext.Current.CancellationToken));
        var paid = await harness.FindInvoiceAsync(invoice.ItemId);

        // Assert
        Assert.Equal((30m, 70m, InvoiceSettlement.PartiallyPaid), (partly.AmountPaid, partly.AmountDue, partly.Settlement));
        Assert.Equal((100m, 0m, InvoiceSettlement.Paid), (paid.AmountPaid, paid.AmountDue, paid.Settlement));
        Assert.Equal(2, paid.History.Count(entry => entry.Kind == DocumentEventKinds.Payment));
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(invoice.ItemId));
    }

    [Fact]
    public async Task APaymentTakenBack_MakesTheInvoiceOwedAgain()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var payment = await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 100m }, TestContext.Current.CancellationToken));

        // Act
        await harness.OnLedgerAsync(ledger => ledger.ReverseAsync(invoice.TransactionId, payment.Event.Id, "The check bounced", TestContext.Current.CancellationToken));

        // Assert
        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((0m, 100m, InvoiceSettlement.Unpaid), (stored.AmountPaid, stored.AmountDue, stored.Settlement));
        Assert.Contains(stored.History, entry => entry.Kind == DocumentEventKinds.Payment && entry.Amount == -100m && entry.Message.Contains("The check bounced", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MirroringAgain_AddsEachLedgerEntryOnce()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken));

        // Act
        await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceLedgerSync>().SyncAsync(await harness.FindInvoiceAsync(invoice.ItemId), TestContext.Current.CancellationToken));
        await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceLedgerSync>().SyncAsync(await harness.FindInvoiceAsync(invoice.ItemId), TestContext.Current.CancellationToken));

        // Assert
        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Single(stored.History, entry => entry.Kind == DocumentEventKinds.Payment);
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(invoice.ItemId));
    }

    /// <summary>
    /// A payment recorded while an invoice command holds the invoice is not mirrored then; opening the invoice catches up.
    /// </summary>
    [Fact]
    public async Task APaymentMissedWhileTheInvoiceWasBusy_IsCaughtUpWhenTheInvoiceIsOpened()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        await harness.InScopeAsync(async services =>
        {
            var (locker, locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireLockAsync(DefaultInvoiceCommandService.LockPrefix + invoice.ItemId, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
            Assert.True(locked);

            await using (locker)
            {
                await harness.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 25m }, TestContext.Current.CancellationToken));
            }

            return true;
        });

        var missed = await harness.FindInvoiceAsync(invoice.ItemId);

        // Act
        var caughtUp = await harness.InScopeAsync(async services => await services.GetRequiredService<InvoiceLedgerSync>().SyncAsync(await services.GetRequiredService<IInvoiceManager>().FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0m, missed.AmountPaid);
        Assert.Equal((25m, 75m, InvoiceSettlement.PartiallyPaid), (caughtUp.AmountPaid, caughtUp.AmountDue, caughtUp.Settlement));
        Assert.Equal(25m, (await harness.FindInvoiceAsync(invoice.ItemId)).AmountPaid);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }
}
