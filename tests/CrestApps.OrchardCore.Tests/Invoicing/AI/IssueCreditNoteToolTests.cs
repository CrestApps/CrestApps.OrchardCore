using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>issueCreditNote</c> issues only the draft <c>previewCreditNote</c> prepared, with the preview's token, total,
/// currency and disposition: it is refused without the permission, without the token, with other amounts, and after the
/// draft was changed on its page; issued, it lowers what the ledger has owed, and the token works once.
/// </summary>
public sealed class IssueCreditNoteToolTests
{
    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Issue_IsRefused_WithoutThePermission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (_, preview) = await PreviewAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), Issue(preview), InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageCreditNotes));

        // Assert
        Assert.Contains("not allowed", answer, StringComparison.Ordinal);
        Assert.Equal(CreditNoteStatus.Draft, (await FindAsync(harness, preview.CreditNoteId)).Status);
    }

    [Fact]
    public async Task Issue_WithoutTheToken_OrWithAnotherTotal_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (_, preview) = await PreviewAsync(harness);
        var noToken = Issue(preview);
        noToken.Remove("previewToken");
        var otherTotal = Issue(preview);
        otherTotal["total"] = 45m;

        // Act
        var missing = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), noToken);
        var different = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), otherTotal);

        // Assert
        Assert.Contains("call previewCreditNote first", missing, StringComparison.Ordinal);
        Assert.Contains("not what was given", different, StringComparison.Ordinal);
        Assert.Equal(CreditNoteStatus.Draft, (await FindAsync(harness, preview.CreditNoteId)).Status);
    }

    [Fact]
    public async Task Issue_AfterTheDraftWasChangedOnItsPage_IsRefused()
    {
        // Arrange: someone edits the draft after the preview, keeping its total.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (_, preview) = await PreviewAsync(harness);
        await harness.InScopeAsync(async services =>
        {
            var store = services.GetRequiredService<ICreditNoteStore>();
            var draft = await store.FindByIdAsync(preview.CreditNoteId, TestContext.Current.CancellationToken);
            var line = Assert.Single(draft.Lines);
            line.Quantity = 2m;
            line.UnitPrice = 20m;
            await store.UpdateAsync(draft, TestContext.Current.CancellationToken);

            return true;
        });

        // Act
        var answer = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), Issue(preview));

        // Assert
        Assert.Contains("issued for other arguments, or the document changed", answer, StringComparison.Ordinal);
        Assert.Equal(CreditNoteStatus.Draft, (await FindAsync(harness, preview.CreditNoteId)).Status);
    }

    [Fact]
    public async Task Issue_LowersWhatTheLedgerHasOwed_AndTheTokenWorksOnce()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var (invoice, preview) = await PreviewAsync(harness);

        // Act
        var issued = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), Issue(preview));
        var again = await _context.InvokeAsync(harness, new IssueCreditNoteTool(), Issue(preview));

        // Assert
        using var json = JsonDocument.Parse(issued);
        Assert.Equal(40m, json.RootElement.GetProperty("applied").GetDecimal());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("number").GetString()));

        var creditNote = await FindAsync(harness, preview.CreditNoteId);
        Assert.Equal((CreditNoteStatus.Issued, 40m), (creditNote.Status, creditNote.AmountApplied));
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);

        // Issued already: nothing moves, and the used token is not asked for.
        Assert.Contains("issued already", again, StringComparison.Ordinal);
        Assert.Equal(60m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    private async Task<(Invoice Invoice, Preview Preview)> PreviewAsync(InvoicingTestHarness harness)
    {
        var invoice = await InvoicingCreditToolContext.IssueInvoiceAsync(harness);
        var answer = await _context.InvokeAsync(harness, new PreviewCreditNoteTool(), new()
        {
            ["invoice"] = invoice.Number,
            ["currency"] = "USD",
            ["disposition"] = "reduceBalance",
            ["lines"] = JsonSerializer.SerializeToElement(new[] { new { invoiceLineId = "line-1", amount = 40m } }),
        });

        using var json = JsonDocument.Parse(answer);
        var creditNote = json.RootElement.GetProperty("creditNote");

        return (invoice, new Preview(
            creditNote.GetProperty("id").GetString(),
            creditNote.GetProperty("total").GetDecimal(),
            json.RootElement.GetProperty("previewToken").GetString()));
    }

    private static Dictionary<string, object> Issue(Preview preview)
        => new()
        {
            ["creditNoteId"] = preview.CreditNoteId,
            ["total"] = preview.Total,
            ["currency"] = "USD",
            ["disposition"] = "reduceBalance",
            ["previewToken"] = preview.Token,
        };

    private static Task<CreditNote> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private sealed record Preview(string CreditNoteId, decimal Total, string Token);
}
