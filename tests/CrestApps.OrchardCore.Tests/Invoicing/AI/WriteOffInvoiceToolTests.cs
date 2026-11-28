using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>writeOffInvoice</c> writes off what the ledger still has owed, only with the token <c>previewInvoiceAction</c> issued
/// for the same invoice and amount, and only for a caller who may void invoices.
/// </summary>
public sealed class WriteOffInvoiceToolTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task WriteOffInvoice_RefusesACallerWithoutVoidInvoices_AndOneWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 100m);

        // Act
        var refused = await _context.InvokeAsync(harness, new WriteOffInvoiceTool(), Arguments(invoice, 100m, token), allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.VoidInvoices));
        var anonymous = await _context.InvokeAsync(harness, new WriteOffInvoiceTool(), Arguments(invoice, 100m, token), signedIn: false);

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task WriteOffInvoice_WritesOffWhatTheLedgerStillHasOwed()
    {
        // Arrange: 30 USD of the 100 USD was paid, so 70 USD is written off.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(invoice.TransactionId, 30m, "chk-1", null, TestContext.Current.CancellationToken));
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        var token = await PreviewAsync(harness, invoice, 70m);

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new WriteOffInvoiceTool(), Arguments(invoice, 70m, token)));

        // Assert
        Assert.Equal(("uncollectible", 70m), (answer.GetProperty("status").GetString(), answer.GetProperty("writtenOff").GetDecimal()));
        Assert.Equal(InvoiceStatus.Uncollectible, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
        Assert.Equal(0m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task WriteOffInvoice_RefusesATokenWhenThePaymentsChangedSinceThePreview()
    {
        // Arrange: previewed at 100 USD owed, then 30 USD was paid.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 100m);
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(invoice.TransactionId, 30m, "chk-1", null, TestContext.Current.CancellationToken));
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));

        // Act
        var stale = await _context.InvokeAsync(harness, new WriteOffInvoiceTool(), Arguments(invoice, 100m, token));

        // Assert
        Assert.StartsWith("Nothing was written off", stale, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task WriteOffInvoice_NeedsAReason()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 100m);
        var arguments = Arguments(invoice, 100m, token);
        arguments.Remove("reason");

        // Act
        var answer = await _context.InvokeAsync(harness, new WriteOffInvoiceTool(), arguments);

        // Assert
        Assert.Contains("say why the invoice is written off", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    private async Task<string> PreviewAsync(InvoicingTestHarness harness, Invoice invoice, decimal amount)
    {
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), PreviewInvoiceActionToolTests.Arguments(invoice, "writeOff", amount)));

        return preview.GetProperty("previewToken").GetString();
    }

    private static Dictionary<string, object> Arguments(Invoice invoice, decimal amount, string token)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["amount"] = amount,
            ["currency"] = "USD",
            ["reason"] = "The customer closed.",
            ["previewToken"] = token,
        };
}
