using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Deployments;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An open invoice moved to another site with its transaction can be paid there; without its transaction it is shown as
/// not payable here and never throws. The ledger travels as it is, never posted again.
/// </summary>
public sealed class InvoiceTransportTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnOpenPartPaidInvoice_MovedWithItsTransaction_OwesTheSame_TakesAPayment_AndKeepsItsNumber(bool transactionsFirst)
    {
        // Arrange: on one site, an invoice of 100 with 40 paid.
        await using var source = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(source);
        await source.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 40m }, TestContext.Current.CancellationToken));

        var (ledgerStep, invoiceJson) = await source.InScopeAsync(async services => (
            await Exporter(services).ExportAsync([invoice.TransactionId], TestContext.Current.CancellationToken),
            InvoicingDocumentExport.Export(await services.GetRequiredService<IInvoiceStore>().FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken))));

        await using var target = await InvoicingTestHarness.CreateAsync();

        // Act: one recipe, in either order.
        await target.InScopeAsync(async services =>
        {
            if (transactionsFirst)
            {
                await ImportLedgerAsync(services, ledgerStep);
                await ImportInvoiceAsync(services, invoiceJson);
            }
            else
            {
                await ImportInvoiceAsync(services, invoiceJson);
                await ImportLedgerAsync(services, ledgerStep);
            }

            return true;
        });

        // Assert: the same amount due on both records, nothing posted or numbered on the way in.
        var moved = await target.FindInvoiceAsync(invoice.ItemId);
        var transaction = await target.FindTransactionAsync(invoice.TransactionId);
        Assert.Equal((invoice.Number, 60m, InvoiceSettlement.PartiallyPaid), (moved.Number, moved.AmountDue, moved.Settlement));
        Assert.Equal((40m, 60m), (transaction.AmountPaid, transaction.OutstandingAmount));
        Assert.Equal(0, target.Numbers.Issued);
        Assert.Empty(target.Observed);
        Assert.True(await target.InScopeAsync(services => services.GetRequiredService<InvoiceLedgerAvailability>().CanPayAsync(moved, TestContext.Current.CancellationToken)));

        // It takes the rest of its payment there...
        var paid = await target.OnLedgerAsync(ledger => ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest { Amount = 60m }, TestContext.Current.CancellationToken));
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        Assert.Equal((0m, InvoiceSettlement.Paid), ((await target.FindInvoiceAsync(invoice.ItemId)).AmountDue, (await target.FindInvoiceAsync(invoice.ItemId)).Settlement));

        // ...and the site's own series passes over its number.
        var next = await IssueAsync(target);
        Assert.NotEqual(invoice.Number, next.Number);
    }

    [Fact]
    public async Task AnOpenInvoiceWithoutItsTransaction_CantBePaidHere_LogsOnce_AndNeverThrows()
    {
        // Arrange: the invoice comes in, its transaction doesn't.
        await using var source = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(source);
        var json = InvoicingDocumentExport.Export(await source.FindInvoiceAsync(invoice.ItemId));

        await using var target = await InvoicingTestHarness.CreateAsync();
        await target.InScopeAsync(async services =>
        {
            await ImportInvoiceAsync(services, json);

            return true;
        });

        var logger = new CountingLogger();

        // Act
        var (missing, canPay, payable, synced) = await target.InScopeAsync(async services =>
        {
            var availability = new InvoiceLedgerAvailability(services.GetRequiredService<ITransactionManager>(), logger);
            var stored = await services.GetRequiredService<IInvoiceStore>().FindByIdAsync(invoice.ItemId, TestContext.Current.CancellationToken);

            return (
                await availability.IsMissingAsync(stored, TestContext.Current.CancellationToken),
                await availability.CanPayAsync(stored, TestContext.Current.CancellationToken),
                await availability.PayableAsync([stored], TestContext.Current.CancellationToken),
                await services.GetRequiredService<InvoiceLedgerSync>().SyncAsync(stored, TestContext.Current.CancellationToken));
        });

        var voided = await target.RunAsync(commands => commands.VoidAsync(invoice.ItemId, "Moved by mistake", TestContext.Current.CancellationToken));
        var writtenOff = await target.RunAsync(commands => commands.WriteOffAsync(invoice.ItemId, "Gone", TestContext.Current.CancellationToken));

        // Assert
        Assert.True(missing);
        Assert.False(canPay);
        Assert.Empty(payable);
        Assert.Equal(100m, synced.AmountDue);
        Assert.Equal(1, logger.Warnings);
        Assert.NotNull(voided);
        Assert.NotNull(writtenOff);
        Assert.Equal((InvoiceStatus.Open, 100m), ((await target.FindInvoiceAsync(invoice.ItemId)).Status, (await target.FindInvoiceAsync(invoice.ItemId)).AmountDue));
    }

    [Fact]
    public async Task TheLedgerImport_KeepsATransactionTheSiteHas_AndSaysWhenPaymentsNeedCheckout()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var step = await harness.InScopeAsync(services => Exporter(services).ExportAsync([invoice.TransactionId], TestContext.Current.CancellationToken));
        var transaction = (JsonObject)((JsonArray)step[TransactionLedgerStep.Transactions])[0];
        transaction["TotalAmount"] = 999m;
        step[TransactionLedgerStep.PaymentAttempts] = new JsonArray(new JsonObject { ["ItemId"] = "attempt-1" });

        // Act
        var result = await harness.InScopeAsync(services => Importer(services).ImportAsync(step, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((0, 1), (result.Imported, result.Kept));
        Assert.Single(result.Errors);
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).TotalAmount);
        Assert.False(transaction.ContainsKey("Id"));
    }

    private static TransactionLedgerExporter Exporter(IServiceProvider services)
        => new(services.GetRequiredService<ITransactionStore>(), services);

    private static TransactionLedgerImporter Importer(IServiceProvider services)
        => new(services.GetRequiredService<ITransactionStore>(), services, NullLogger<TransactionLedgerImporter>.Instance, new Telephony.Doubles.PassThroughStringLocalizer<TransactionLedgerImporter>());

    private static async Task ImportLedgerAsync(IServiceProvider services, JsonObject step)
    {
        var result = await Importer(services).ImportAsync((JsonObject)step.DeepClone(), TestContext.Current.CancellationToken);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Imported);
    }

    private static async Task ImportInvoiceAsync(IServiceProvider services, JsonObject invoice)
    {
        var result = await services.GetRequiredService<InvoicingDocumentImporter>().ImportInvoicesAsync([(JsonObject)invoice.DeepClone()], TestContext.Current.CancellationToken);

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Imported);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private sealed class CountingLogger : ILogger<InvoiceLedgerAvailability>
    {
        public int Warnings { get; private set; }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings++;
            }
        }
    }
}
