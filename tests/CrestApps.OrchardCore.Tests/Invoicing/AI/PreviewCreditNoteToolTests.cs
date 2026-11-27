using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>previewCreditNote</c> lists what an issued invoice can still be credited by, or saves the credit note asked for as a
/// draft through the credit note service and returns a token for <c>issueCreditNote</c>. It applies the issue's limits
/// first: a credit past what is left, everything without <c>creditAll</c>, a refund of more than was paid and another
/// person's draft are refused, and a draft that breaks a rule is not kept.
/// </summary>
public sealed class PreviewCreditNoteToolTests
{
    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Preview_IsRefused_WithoutThePermission_OrWhenNobodyIsSignedIn()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var arguments = PartOfTheLine(invoice, 40m);

        // Act
        var denied = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), arguments, InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageCreditNotes));
        var anonymous = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), arguments, signedIn: false);

        // Assert
        Assert.Contains("not allowed", denied, StringComparison.Ordinal);
        Assert.Contains("not allowed", anonymous, StringComparison.Ordinal);
        Assert.Empty(await ListCreditNotesAsync(harness, invoice));
    }

    [Fact]
    public async Task Preview_WithOnlyTheInvoice_ListsWhatIsLeftToCredit()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), new() { ["invoice"] = invoice.Number });

        // Assert
        using var json = JsonDocument.Parse(answer);
        var creditable = json.RootElement.GetProperty("creditable");
        Assert.Equal(100m, creditable.GetProperty("total").GetDecimal());
        var line = Assert.Single(creditable.GetProperty("lines").EnumerateArray());
        Assert.Equal(("line-1", 100m), (line.GetProperty("invoiceLineId").GetString(), line.GetProperty("leftToCredit").GetDecimal()));
        Assert.False(json.RootElement.TryGetProperty("previewToken", out _));
        Assert.Empty(await ListCreditNotesAsync(harness, invoice));
    }

    [Fact]
    public async Task Preview_OfPartOfALine_SavesTheDraft_AndReturnsAToken()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 40m));

        // Assert
        using var json = JsonDocument.Parse(answer);
        var creditNote = json.RootElement.GetProperty("creditNote");
        Assert.Equal(40m, creditNote.GetProperty("total").GetDecimal());
        Assert.Equal("reduceBalance", creditNote.GetProperty("disposition").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("previewToken").GetString()));
        Assert.DoesNotContain("history", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internalNote", answer, StringComparison.OrdinalIgnoreCase);

        var draft = Assert.Single(await ListCreditNotesAsync(harness, invoice));
        Assert.Equal((CreditNoteStatus.Draft, 40m, "Damaged on delivery"), (draft.Status, draft.Totals.Total, draft.Reason));
        Assert.Equal("line-1", Assert.Single(draft.Lines).CreditedLineId);
    }

    [Fact]
    public async Task Preview_OfEverything_WithoutCreditAll_IsRefused_AndNoDraftIsKept()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 100m));

        // Assert
        Assert.Contains("creditAll", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("previewToken", answer, StringComparison.Ordinal);
        Assert.Empty(await ListCreditNotesAsync(harness, invoice));
    }

    [Fact]
    public async Task Preview_OfMoreThanIsLeft_OrInAnotherCurrency_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var euros = PartOfTheLine(invoice, 40m);
        euros["currency"] = "EUR";

        // Act
        var tooMuch = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 150m));
        var wrongCurrency = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), euros);

        // Assert
        Assert.Contains("at most", tooMuch, StringComparison.Ordinal);
        Assert.Contains("give currency USD", wrongCurrency, StringComparison.Ordinal);
        Assert.Empty(await ListCreditNotesAsync(harness, invoice));
    }

    [Fact]
    public async Task Preview_OfARefund_OfMoreThanWasPaid_IsRefused_AndNoDraftIsKept()
    {
        // Arrange: nothing was paid on the invoice.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var refund = PartOfTheLine(invoice, 40m);
        refund["disposition"] = "refund";

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), refund);

        // Assert
        Assert.Contains("can't be refunded", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("previewToken", answer, StringComparison.Ordinal);
        Assert.Empty(await ListCreditNotesAsync(harness, invoice));
    }

    [Fact]
    public async Task Preview_LeavesADraftSomeoneStarted_UnlessReplaceDraftNamesIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var started = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));
        var replace = PartOfTheLine(invoice, 40m);
        replace["replaceDraft"] = started.CreditNote.ItemId;

        // Act
        var refused = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 40m));
        var replaced = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), replace);

        // Assert
        Assert.Contains("already has a draft credit note", refused, StringComparison.Ordinal);
        Assert.Contains("previewToken", replaced, StringComparison.Ordinal);
        var draft = Assert.Single(await ListCreditNotesAsync(harness, invoice));
        Assert.Equal(started.CreditNote.ItemId, draft.ItemId);
        Assert.Equal((40m, "Damaged on delivery"), (draft.Totals.Total, draft.Reason));
    }

    [Fact]
    public async Task Preview_Again_ReplacesTheDraftTheCallersEarlierPreviewStarted()
    {
        // Arrange: the preview's message tells the model to preview again in a later message, for a new token.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var first = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 40m));
        var firstDraft = Assert.Single(await ListCreditNotesAsync(harness, invoice));

        // Act
        var again = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), PartOfTheLine(invoice, 30m));

        // Assert: no "already has a draft" refusal; the same draft now holds the new credit.
        Assert.Contains("previewToken", first, StringComparison.Ordinal);
        Assert.DoesNotContain("already has a draft credit note", again, StringComparison.Ordinal);
        Assert.Contains("previewToken", again, StringComparison.Ordinal);
        var draft = Assert.Single(await ListCreditNotesAsync(harness, invoice));
        Assert.Equal(firstDraft.ItemId, draft.ItemId);
        Assert.Equal(30m, draft.Totals.Total);
    }

    [Fact]
    public async Task Preview_ThatWouldReplaceADraft_ButBreaksARule_LeavesTheDraftAsItWas()
    {
        // Arrange: a draft of the whole invoice someone started; the replacement refunds more than was paid.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var started = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));
        var replace = PartOfTheLine(invoice, 40m);
        replace["disposition"] = "refund";
        replace["replaceDraft"] = started.CreditNote.ItemId;

        // Act
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), replace);

        // Assert
        Assert.Contains("can't be refunded", answer, StringComparison.Ordinal);
        Assert.Contains("was left as it was", answer, StringComparison.Ordinal);
        var draft = Assert.Single(await ListCreditNotesAsync(harness, invoice));
        Assert.Equal(started.CreditNote.ItemId, draft.ItemId);
        Assert.Equal((CreditNoteDisposition.ReduceBalance, 100m, (string)null), (draft.Disposition, draft.Totals.Total, draft.Reason));
        Assert.Equal(started.CreditNote.Lines.Select(line => line.Id), draft.Lines.Select(line => line.Id));
    }

    private static Dictionary<string, object> PartOfTheLine(Invoice invoice, decimal amount)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["currency"] = "USD",
            ["disposition"] = "reduceBalance",
            ["reason"] = "Damaged on delivery",
            ["lines"] = JsonSerializer.SerializeToElement(new[] { new { invoiceLineId = "line-1", amount } }),
        };

    private static Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(InvoicingTestHarness harness, Invoice invoice)
        => harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteStore>().ListByInvoiceAsync(invoice.ItemId, TestContext.Current.CancellationToken));
}
