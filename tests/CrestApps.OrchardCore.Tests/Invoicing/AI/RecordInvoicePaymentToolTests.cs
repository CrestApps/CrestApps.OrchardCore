using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>recordInvoicePayment</c> records money received outside the site on the invoice's ledger entry, up to what is
/// still owed, only with the token <c>previewInvoiceAction</c> issued for the same payment, and once per idempotency key.
/// </summary>
public sealed class RecordInvoicePaymentToolTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task RecordInvoicePayment_RefusesACallerWithoutRecordInvoicePayments_AndOneWhoIsNotSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);

        // Act
        var refused = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"), allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.RecordInvoicePayments));
        var anonymous = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"), signedIn: false);

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task RecordInvoicePayment_RecordsThePayment_AndTheInvoiceFollows()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1")));

        // Assert
        Assert.Equal((40m, 60m), (answer.GetProperty("recorded").GetDecimal(), answer.GetProperty("amountDue").GetDecimal()));
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((60m, InvoiceSettlement.PartiallyPaid), (stored.AmountDue, stored.Settlement));
    }

    [Fact]
    public async Task RecordInvoicePayment_SettlesTheInvoice_WithEntireBalance()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 100m, entireBalance: true);

        // Act
        var answer = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 100m, token, "key-1", entireBalance: true));

        // Assert
        Assert.DoesNotContain("Nothing was recorded", answer, StringComparison.Ordinal);
        Assert.Equal(InvoiceSettlement.Paid, (await harness.FindInvoiceAsync(invoice.ItemId)).Settlement);
    }

    [Fact]
    public async Task RecordInvoicePayment_RecordsOnce_ForARepeatedKey()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);
        var first = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"));

        // Act: the same call again, as a retry would send it.
        var again = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"));

        // Assert
        Assert.DoesNotContain("Nothing was recorded", first, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was recorded again", again, StringComparison.Ordinal);
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task RecordInvoicePayment_RefusesAUsedToken_WithANewKey()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);
        await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"));

        // Act: the state changed (60 USD owed now), so the old preview no longer matches.
        var reused = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-2"));

        // Assert
        Assert.StartsWith("Nothing was recorded", reused, StringComparison.Ordinal);
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task RecordInvoicePayment_RefusesATokenPreviewedForAnotherAmount_AndKeepsTheKeyFree()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);

        // Act
        var mismatch = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 30m, token, "key-1"));
        var retried = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Arguments(invoice, 40m, token, "key-1"));

        // Assert
        Assert.Contains("issued for other arguments", mismatch, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing was recorded", retried, StringComparison.Ordinal);
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task RecordInvoicePayment_NeedsAnIdempotencyKey_AndRefusesAnAccountNumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var token = await PreviewAsync(harness, invoice, 40m);
        var noKey = Arguments(invoice, 40m, token, null);
        noKey.Remove("idempotencyKey");
        var withCard = Arguments(invoice, 40m, token, "key-1");
        withCard["note"] = "Paid with 4111-1111-1111-1111";

        // Act
        var keyless = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), noKey);
        var card = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), withCard);

        // Assert
        Assert.Contains("idempotencyKey", keyless, StringComparison.Ordinal);
        Assert.Contains("card or bank account number", card, StringComparison.Ordinal);
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    private async Task<string> PreviewAsync(InvoicingTestHarness harness, Invoice invoice, decimal amount, bool entireBalance = false)
    {
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), PreviewInvoiceActionToolTests.Payment(invoice, amount, entireBalance: entireBalance)));

        return preview.GetProperty("previewToken").GetString();
    }

    private static Dictionary<string, object> Arguments(Invoice invoice, decimal amount, string token, string key, bool entireBalance = false)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["amount"] = amount,
            ["currency"] = "USD",
            ["entireBalance"] = entireBalance,
            ["method"] = "check",
            ["reference"] = "chk-4471",
            ["previewToken"] = token,
            ["idempotencyKey"] = key,
        };
}
