using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Deployments;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Invoices and credit notes move between sites in recipes as legal records: an issued document keeps its number and
/// never posts to the ledger again, one the site already issued is left as it is, no number is held twice, and the site's
/// own series passes over the numbers that came in.
/// </summary>
public sealed class InvoicingImportTests
{
    [Fact]
    public async Task AnExportedInvoiceAndCreditNote_ImportWithTheirNumbers_AndPostNothing()
    {
        // Arrange: an issued invoice credited in part on one site.
        await using var source = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(source);
        var creditNote = await CreditAsync(source, invoice);
        var invoiceJson = InvoicingDocumentExport.Export(await source.FindInvoiceAsync(invoice.ItemId));
        var creditNoteJson = InvoicingDocumentExport.Export(creditNote);

        await using var target = await InvoicingTestHarness.CreateAsync();

        // Act
        var (invoices, creditNotes) = await target.InScopeAsync(async services =>
        {
            var importer = services.GetRequiredService<InvoicingDocumentImporter>();

            return (
                await importer.ImportInvoicesAsync([invoiceJson], TestContext.Current.CancellationToken),
                await importer.ImportCreditNotesAsync([creditNoteJson], TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal((1, 0, 1, 0), (invoices.Imported, invoices.Errors.Count, creditNotes.Imported, creditNotes.Errors.Count));
        Assert.False(invoiceJson.ContainsKey("Id"));

        var imported = await target.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal((invoice.Number, InvoiceStatus.Open, invoice.TransactionId, 100m), (imported.Number, imported.Status, imported.TransactionId, imported.AmountCredited));
        Assert.Equal(invoice.IssuedUtc, imported.IssuedUtc);
        Assert.Contains(imported.History, entry => entry.Kind == DocumentEventKinds.Issued);
        Assert.Null(await target.FindTransactionAsync(invoice.TransactionId));
        Assert.Equal(0, target.Numbers.Issued);

        var importedNote = await target.InScopeAsync(services => services.GetRequiredService<ICreditNoteStore>().FindByIdAsync(creditNote.ItemId, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal((creditNote.Number, CreditNoteStatus.Issued, invoice.ItemId), (importedNote.Number, importedNote.Status, importedNote.InvoiceId));
    }

    [Fact]
    public async Task AnInvoiceTheSiteAlreadyIssued_IsKeptAsItIs()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);
        var json = InvoicingDocumentExport.Export(invoice);
        json["AmountDue"] = 999m;
        json["Notes"] = "Changed by a recipe";

        // Act
        var result = await harness.InScopeAsync(services => services.GetRequiredService<InvoicingDocumentImporter>().ImportInvoicesAsync([json], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((0, 1, 0), (result.Imported, result.Kept, result.Errors.Count));
        var stored = await harness.FindInvoiceAsync(invoice.ItemId);
        Assert.Equal(100m, stored.AmountDue);
        Assert.NotEqual("Changed by a recipe", stored.Notes);
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(invoice.ItemId));
    }

    [Fact]
    public async Task ADraftTheSiteHas_IsReplaced_InTheSameDocument()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();
        var json = InvoicingDocumentExport.Export(draft);
        json["Notes"] = "From the recipe";

        // Act
        var result = await harness.InScopeAsync(services => services.GetRequiredService<InvoicingDocumentImporter>().ImportInvoicesAsync([json], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, result.Imported);
        Assert.Equal("From the recipe", (await harness.FindInvoiceAsync(draft.ItemId)).Notes);
        Assert.Equal(1, await harness.CountInvoiceDocumentsAsync(draft.ItemId));
    }

    [Fact]
    public async Task ANumberAnotherInvoiceHolds_IsRefused_AndAnIssuedInvoiceNeedsANumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(harness);

        var sameNumber = InvoicingDocumentExport.Export(invoice);
        sameNumber["ItemId"] = "4other000000000000000000aa";

        var noNumber = InvoicingDocumentExport.Export(invoice);
        noNumber["ItemId"] = "4nonumber00000000000000aaa";
        noNumber["Number"] = null;

        // Act
        var result = await harness.InScopeAsync(services => services.GetRequiredService<InvoicingDocumentImporter>().ImportInvoicesAsync([sameNumber, noNumber, new JsonObject { ["Number"] = "X" }], TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains(invoice.Number, StringComparison.Ordinal));
        Assert.Null(await harness.FindInvoiceAsync("4other000000000000000000aa"));
    }

    [Fact]
    public async Task Issuing_PassesOverANumberAnImportedInvoiceHolds()
    {
        // Arrange: the other site's first invoice comes in as INV-1001, the number this site would give next.
        await using var source = await InvoicingTestHarness.CreateAsync();
        var invoice = await IssueAsync(source);
        Assert.Equal("INV-1001", invoice.Number);

        await using var target = await InvoicingTestHarness.CreateAsync();
        await target.InScopeAsync(services => services.GetRequiredService<InvoicingDocumentImporter>().ImportInvoicesAsync([InvoicingDocumentExport.Export(invoice)], TestContext.Current.CancellationToken));
        var draft = await target.CreateDraftAsync();

        // Act
        var issued = await target.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal("INV-1002", issued.Invoice.Number);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness)
    {
        var draft = await harness.CreateDraftAsync();
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static async Task<CreditNote> CreditAsync(InvoicingTestHarness harness, Invoice invoice)
    {
        var draft = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, TestContext.Current.CancellationToken));
        var issued = await harness.InScopeAsync(services => services.GetRequiredService<ICreditNoteCommandService>().IssueAsync(draft.CreditNote.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.CreditNote;
    }
}
