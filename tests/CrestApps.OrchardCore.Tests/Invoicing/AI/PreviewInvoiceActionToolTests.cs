using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>previewInvoiceAction</c> changes nothing, needs the permission of the action it previews, and issues a token only
/// for an action the action's tool would accept: the money rules (the owner's review, F23) refuse the rest.
/// </summary>
public sealed class PreviewInvoiceActionToolTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task PreviewInvoiceAction_NeedsThePermissionOfTheAction()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);

        // Act
        var voidRefused = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Arguments(invoice, "void", 100m), allow: InvoicingLifecycleToolContext.AllBut(InvoicingPermissions.VoidInvoices));
        var paymentRefused = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, 40m), allow: InvoicingLifecycleToolContext.Only(InvoicingPermissions.VoidInvoices));
        var anonymous = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Arguments(invoice, "void", 100m), signedIn: false);

        // Assert
        Assert.Contains("not allowed", voidRefused, StringComparison.Ordinal);
        Assert.Contains("not allowed", paymentRefused, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewInvoiceAction_ShowsThePayment_AndChangesNothing()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, 40m)));

        // Assert
        Assert.Equal(100m, answer.GetProperty("owedOnLedger").GetDecimal());
        Assert.Equal(60m, answer.GetProperty("after").GetProperty("amountDue").GetDecimal());
        Assert.Equal("partiallyPaid", answer.GetProperty("after").GetProperty("settlement").GetString());
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("previewToken").GetString()));
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Theory]
    [InlineData("void", 50, "USD", null, "the void cancels everything still owed")]
    [InlineData("void", 100, "EUR", null, "give the amount in USD")]
    [InlineData("writeOff", 99.99, "USD", null, "a write-off takes everything still owed")]
    [InlineData("recordPayment", 150, "USD", null, "only 100.00 USD is still owed")]
    [InlineData("recordPayment", 100, "USD", null, "Set entireBalance to true")]
    [InlineData("recordPayment", 10.001, "USD", null, "more decimal places")]
    [InlineData("recordPayment", 0, "USD", null, "more than zero")]
    [InlineData("recordPayment", 40, "USD", "4111 1111 1111 1111", "card or bank account number")]
    [InlineData("recordPayment", 40, "USD", "DE89370400440532013000", "card or bank account number")]
    public async Task PreviewInvoiceAction_RefusesWhatTheActionWouldRefuse_AndIssuesNoToken(string action, double amount, string currency, string reference, string expected)
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var arguments = action == "recordPayment" ? Payment(invoice, (decimal)amount, reference) : Arguments(invoice, action, (decimal)amount);
        arguments["currency"] = currency;

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), arguments);

        // Assert
        Assert.StartsWith("Nothing was previewed", answer, StringComparison.Ordinal);
        Assert.Contains(expected, answer, StringComparison.Ordinal);
        Assert.DoesNotContain("previewToken", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewInvoiceAction_RefusesToVoidAnInvoiceSomethingWasPaidOn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var paid = await harness.OnLedgerAsync(ledger => ledger.RecordOfflinePaymentAsync(invoice.TransactionId, 10m, "chk-1", null, TestContext.Current.CancellationToken));
        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Arguments(invoice, "void", 90m));

        // Assert
        Assert.Contains("something was paid", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewInvoiceAction_RefusesADraft()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), new()
        {
            ["invoice"] = draft.ItemId,
            ["action"] = "recordPayment",
            ["amount"] = 40m,
            ["currency"] = "USD",
            ["method"] = "check",
        });

        // Assert
        Assert.Contains("only an open invoice takes a payment", answer, StringComparison.Ordinal);
    }

    internal static Dictionary<string, object> Arguments(Invoice invoice, string action, decimal amount)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["action"] = action,
            ["amount"] = amount,
            ["currency"] = "USD",
        };

    internal static Dictionary<string, object> Payment(Invoice invoice, decimal amount, string reference = "chk-4471", bool entireBalance = false)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["action"] = "recordPayment",
            ["amount"] = amount,
            ["currency"] = "USD",
            ["entireBalance"] = entireBalance,
            ["method"] = "check",
            ["reference"] = reference,
        };
}
