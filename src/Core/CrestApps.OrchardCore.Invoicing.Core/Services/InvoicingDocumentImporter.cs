using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Imports invoices and credit notes from a recipe, as a deployment plan wrote them.
/// </summary>
/// <remarks>
/// An issued document keeps its number and everything it says: it is never numbered again and never posts to the ledger
/// (its transaction travels with the ledger's own step). An issued document the site already has is kept as it is, since
/// after issue only the document commands change it. A draft the site has is replaced. A document whose number another
/// document of the site already holds is refused, so no number is ever held twice.
/// </remarks>
public sealed class InvoicingDocumentImporter
{
    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public InvoicingDocumentImporter(
        IInvoiceStore invoices,
        ICreditNoteStore creditNotes,
        ILogger<InvoicingDocumentImporter> logger,
        IStringLocalizer<InvoicingDocumentImporter> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Imports invoices.
    /// </summary>
    /// <param name="entries">The invoices, as JSON objects.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was imported, kept and refused.</returns>
    public Task<InvoicingImportResult> ImportInvoicesAsync(IEnumerable<JsonObject> entries, CancellationToken cancellationToken = default)
        => ImportAsync(
            entries,
            _invoices,
            invoice => invoice.Status != InvoiceStatus.Draft,
            async number => await _invoices.FindByNumberAsync(number, cancellationToken),
            S["invoice"],
            cancellationToken);

    /// <summary>
    /// Imports credit notes.
    /// </summary>
    /// <param name="entries">The credit notes, as JSON objects.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was imported, kept and refused.</returns>
    public Task<InvoicingImportResult> ImportCreditNotesAsync(IEnumerable<JsonObject> entries, CancellationToken cancellationToken = default)
        => ImportAsync(
            entries,
            _creditNotes,
            creditNote => creditNote.Status != CreditNoteStatus.Draft,
            async number => await _creditNotes.FindByNumberAsync(number, cancellationToken),
            S["credit note"],
            cancellationToken);

    private async Task<InvoicingImportResult> ImportAsync<T>(
        IEnumerable<JsonObject> entries,
        ICatalog<T> store,
        Func<T, bool> isIssued,
        Func<string, Task<T>> findByNumber,
        LocalizedString kind,
        CancellationToken cancellationToken)
        where T : InvoicingDocument
    {
        var result = new InvoicingImportResult();

        foreach (var data in entries ?? [])
        {
            if (data is null)
            {
                continue;
            }

            // The document id of the site that exported it means nothing here.
            data.Remove(nameof(InvoicingDocument.Id));

            T imported;

            try
            {
                imported = data.Deserialize<T>(CatalogDeploymentSerializer.Options);
            }
            catch (JsonException ex)
            {
                result.Errors.Add(S["A {0} could not be read: {1}", kind, ex.Message].Value);

                continue;
            }

            if (string.IsNullOrWhiteSpace(imported?.ItemId))
            {
                result.Errors.Add(S["A {0} has no ItemId, so it was not imported.", kind].Value);

                continue;
            }

            if (isIssued(imported) && string.IsNullOrWhiteSpace(imported.Number))
            {
                result.Errors.Add(S["The issued {0} '{1}' has no number, so it was not imported.", kind, imported.ItemId].Value);

                continue;
            }

            var existing = await store.FindByIdAsync(imported.ItemId, cancellationToken);

            if (existing is not null && isIssued(existing))
            {
                // Issued here already: the site's record stands.
                result.Kept++;
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("The {Kind} '{ItemId}' is already issued on this site, so the recipe left it as it is.", kind.Value, imported.ItemId);
                }

                continue;
            }

            if (!string.IsNullOrWhiteSpace(imported.Number) &&
                await findByNumber(imported.Number.Trim()) is { } holder &&
                !string.Equals(holder.ItemId, imported.ItemId, StringComparison.Ordinal))
            {
                result.Errors.Add(S["The number {0} is already held by another {1} on this site, so '{2}' was not imported.", imported.Number, kind, imported.ItemId].Value);

                continue;
            }

            if (existing is null)
            {
                await store.CreateAsync(imported, cancellationToken);
            }
            else
            {
                // The draft the site has takes everything the recipe says, on the instance the session tracks.
                CatalogDeploymentSerializer.Populate(existing, data);

                await store.UpdateAsync(existing, cancellationToken);
            }

            result.Imported++;
        }

        return result;
    }
}

/// <summary>
/// What an import of invoicing documents did.
/// </summary>
public sealed class InvoicingImportResult
{
    /// <summary>
    /// Gets or sets how many documents were created or replaced.
    /// </summary>
    public int Imported { get; set; }

    /// <summary>
    /// Gets or sets how many issued documents the site already had and kept.
    /// </summary>
    public int Kept { get; set; }

    /// <summary>
    /// Gets why some documents were not imported.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
