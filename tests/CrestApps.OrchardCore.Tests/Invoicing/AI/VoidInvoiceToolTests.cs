using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>voidInvoice</c> voids only with the token <c>previewInvoiceAction</c> issued to the same caller for the same
/// invoice and amount, used once and before it expires; without the permission, or without a valid token, nothing is
/// voided.
/// </summary>
public sealed class VoidInvoiceToolTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task VoidInvoice_RefusesACallerWithoutVoidInvoices_AndOneWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice);

        // Act
        var refused = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, token), allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.VoidInvoices));
        var anonymous = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, token), signedIn: false);

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_VoidsWithThePreviewsToken_AndCancelsTheLedgerEntry()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice);

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, token)));

        // Assert
        Assert.Equal("void", answer.GetProperty("status").GetString());
        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal(InvoiceStatus.Void, stored.Status);
        Assert.Contains("Duplicate", stored.VoidReason, StringComparison.Ordinal);
        Assert.Equal(TransactionStatus.Canceled, (await harness.FindTransactionAsync(invoice.TransactionId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_NeedsAToken()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var arguments = Arguments(invoice, null);
        arguments.Remove("previewToken");

        // Act
        var answer = await _context.InvokeAsync(harness, new VoidInvoiceTool(), arguments);

        // Assert
        Assert.Contains("call previewInvoiceAction first", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_RefusesATokenPreviewedByAnotherUser()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice);

        // Act
        var answer = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, token), userId: "user-2", userName: "sam");

        // Assert
        Assert.Contains("is not one previewInvoiceAction issued to you", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_RefusesATokenThatExpired()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice);

        // Act
        var answer = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, token), services: InvoicingLifecycleToolContext.Later(TimeSpan.FromMinutes(11)));

        // Assert
        Assert.Contains("previewToken expired", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_RefusesATokenPreviewedForAnotherAction()
    {
        // Arrange: a token previewed for a write-off of the same amount.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), PreviewInvoiceActionToolTests.Arguments(invoice, "writeOff", 100m)));

        // Act
        var answer = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(invoice, preview.GetProperty("previewToken").GetString()));

        // Assert
        Assert.Contains("issued for other arguments", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, (await harness.FindInvoiceAsync(invoice.ItemId)).Status);
    }

    [Fact]
    public async Task VoidInvoice_WaivesALateFee_ByVoidingItsDebitNote()
    {
        // Arrange: a debit note is an invoice of its own, with its own ledger entry.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var debitNote = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness, draft =>
        {
            draft.Kind = InvoiceKind.DebitNote;
            draft.Lines[0].Quantity = 1m;
            draft.Lines[0].UnitPrice = 15m;
        });
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), PreviewInvoiceActionToolTests.Arguments(debitNote, "void", 15m)));

        // Act
        var answer = await _context.InvokeAsync(harness, new VoidInvoiceTool(), Arguments(debitNote, preview.GetProperty("previewToken").GetString(), 15m));

        // Assert
        Assert.Contains("waives that fee", preview.GetProperty("notes").EnumerateArray().Select(note => note.GetString()).First(note => note.Contains("fee", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Void, (await harness.FindInvoiceAsync(debitNote.ItemId)).Status);
        Assert.DoesNotContain("Nothing was voided", answer, StringComparison.Ordinal);
    }

    private async Task<string> PreviewAsync(InvoicingTestHarness harness, Invoice invoice)
    {
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), PreviewInvoiceActionToolTests.Arguments(invoice, "void", 100m)));

        return preview.GetProperty("previewToken").GetString();
    }

    private static Dictionary<string, object> Arguments(Invoice invoice, string token, decimal amount = 100m)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["amount"] = amount,
            ["currency"] = "USD",
            ["reason"] = "Duplicate of another invoice.",
            ["previewToken"] = token,
        };
}
