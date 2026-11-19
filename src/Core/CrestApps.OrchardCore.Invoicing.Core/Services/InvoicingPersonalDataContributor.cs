using System.Globalization;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Exports the invoices and credit notes billed to a person and, on erasure, scrubs their contact details from them.
/// </summary>
/// <remarks>
/// A person's documents are those billed to them as a user, to their customer record or to them as a guest, and those
/// billed to one of their e-mails without a business account. Documents billed to a business account are the business's
/// records and are left alone. Erasure removes the bill-to's name,
/// e-mail, phone, address, shipping address and copy addresses, and scrubs the person from notes and history; it keeps
/// numbers, amounts, dates, tax ids and the archived PDF of an issued document, which the business must keep, and says
/// so on the result.
/// </remarks>
public sealed class InvoicingPersonalDataContributor : ICommercePersonalDataContributor
{
    /// <summary>
    /// The kind of an exported or anonymized invoice.
    /// </summary>
    public const string InvoiceRecordKind = "Invoice";

    /// <summary>
    /// The kind of an exported or anonymized credit note.
    /// </summary>
    public const string CreditNoteRecordKind = "CreditNote";

    private const int PageSize = 100;
    private const int MaxDocuments = 10_000;

    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly IPartyMembershipService _membership;

    internal readonly IStringLocalizer S;

    public InvoicingPersonalDataContributor(
        IInvoiceStore invoices,
        ICreditNoteStore creditNotes,
        IPartyMembershipService membership,
        IStringLocalizer<InvoicingPersonalDataContributor> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _membership = membership;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public bool TakesPartInRetentionExpiry => false;

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var invoice in await FindInvoicesAsync(subject, cancellationToken))
        {
            subject.AddName(invoice.BillTo?.Contact?.Name);
            subject.AddEmail(invoice.BillTo?.Contact?.Email);
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var invoice in await FindInvoicesAsync(context.Subject, cancellationToken))
        {
            var record = Export(invoice, InvoiceRecordKind);
            record.Values[nameof(Invoice.Status)] = invoice.Status.ToString();
            record.Values[nameof(Invoice.AmountPaid)] = Amount(invoice.AmountPaid);
            record.Values[nameof(Invoice.AmountDue)] = Amount(invoice.AmountDue);
            record.Values[nameof(Invoice.DueDate)] = invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            context.Export.Records.Add(record);

            foreach (var creditNote in await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken))
            {
                var note = Export(creditNote, CreditNoteRecordKind);
                note.Values[nameof(CreditNote.Status)] = creditNote.Status.ToString();
                note.Values[nameof(CreditNote.InvoiceNumber)] = creditNote.InvoiceNumber;
                context.Export.Records.Add(note);
            }
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var keptIssued = false;

        foreach (var invoice in await FindInvoicesAsync(context.Subject, cancellationToken))
        {
            Anonymize(invoice, context);
            await _invoices.UpdateAsync(invoice, cancellationToken);
            context.Result.Count(InvoiceRecordKind);
            keptIssued |= invoice.Status != InvoiceStatus.Draft;

            foreach (var creditNote in await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken))
            {
                Anonymize(creditNote, context);
                await _creditNotes.UpdateAsync(creditNote, cancellationToken);
                context.Result.Count(CreditNoteRecordKind);
            }
        }

        if (keptIssued)
        {
            context.Result.Warnings.Add(S["The numbers, amounts, dates and tax ids of the person's issued invoices and credit notes, and their archived PDFs, were kept: the business must keep issued invoices."].Value);
        }
    }

    /// <summary>
    /// Removes the person's contact details from one document and scrubs them from its notes and history.
    /// </summary>
    internal static void Anonymize(InvoicingDocument document, CommercePersonalDataAnonymizeContext context)
    {
        if (document.BillTo is { } billTo)
        {
            billTo.Contact = billTo.Contact is null ? null : new Addresses.Models.ContactInfo { Name = context.ReplaceName(billTo.Contact.Name) };
            billTo.ShippingAddress = null;
            billTo.CopyTo = [];
        }

        document.Notes = context.Scrub(document.Notes);
        document.InternalNote = context.Scrub(document.InternalNote);
        document.Reference = context.Scrub(document.Reference);

        foreach (var entry in document.History ?? [])
        {
            entry.Message = context.Scrub(entry.Message);
            entry.ActorName = context.Scrub(entry.ActorName);
        }
    }

    private static CommercePersonalDataRecord Export(InvoicingDocument document, string kind)
    {
        var contact = document.BillTo?.Contact;

        return new CommercePersonalDataRecord
        {
            Kind = kind,
            Id = document.ItemId,
            CreatedUtc = document.CreatedUtc,
            Values =
            {
                [nameof(InvoicingDocument.Number)] = document.Number,
                [nameof(InvoicingDocument.Currency)] = document.Currency,
                ["Total"] = Amount(document.Totals?.Total ?? 0m),
                [nameof(InvoicingDocument.IssueDate)] = document.IssueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["BillToName"] = contact?.Name,
                ["BillToEmail"] = contact?.Email,
                ["BillToPhone"] = contact?.Phone.ToString() is { Length: > 0 } phone ? phone : null,
                ["BillToAddress"] = contact?.Address?.ToString(),
                ["BillToTaxId"] = document.BillTo?.TaxId,
            },
        };
    }

    private static bool IsPersonal(PartyRef party)
        => party.Kind is PartyKinds.User or PartyKinds.Customer or PartyKinds.Guest or PartyKinds.Contact;

    private static string Amount(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    // The person's own parties: them as a user, their customer records, and their guest checkouts. Not the business
    // accounts they act for.
    private async Task<IReadOnlyList<Invoice>> FindInvoicesAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var parties = new HashSet<PartyRef>();

        foreach (var owner in subject.Owners)
        {
            if (owner.Kind == CustomerOwnerKind.Guest)
            {
                parties.Add(new PartyRef(PartyKinds.Guest, owner.Id));

                continue;
            }

            parties.Add(new PartyRef(PartyKinds.User, owner.Id));

            foreach (var party in await _membership.GetPartiesAsync(owner.Id, cancellationToken))
            {
                if (string.Equals(party.Kind, PartyKinds.Customer, StringComparison.Ordinal))
                {
                    parties.Add(party);
                }
            }
        }

        var found = new Dictionary<string, Invoice>(StringComparer.Ordinal);

        // Invoices billed by e-mail alone, or to the person's customer record, found by the e-mails known for them. One
        // billed to a business account under the same e-mail stays the business's.
        foreach (var invoice in await _invoices.ListByBillToEmailAsync(subject.Emails, MaxDocuments, cancellationToken))
        {
            if (invoice.BillTo?.Party is not { IsEmpty: false } party || IsPersonal(party))
            {
                found.TryAdd(invoice.ItemId, invoice);
            }
        }

        if (parties.Count == 0)
        {
            return [.. found.Values.OrderBy(invoice => invoice.CreatedUtc)];
        }

        var query = new InvoiceQuery { BillTo = [.. parties], OnlyBillTo = true };

        for (var page = 1; (page - 1) * PageSize < MaxDocuments; page++)
        {
            var result = await _invoices.PageAsync(page, PageSize, query, cancellationToken);
            var entries = result.Entries?.ToArray() ?? [];

            foreach (var invoice in entries)
            {
                found.TryAdd(invoice.ItemId, invoice);
            }

            if (entries.Length < PageSize)
            {
                break;
            }
        }

        return [.. found.Values.OrderBy(invoice => invoice.CreatedUtc)];
    }
}
