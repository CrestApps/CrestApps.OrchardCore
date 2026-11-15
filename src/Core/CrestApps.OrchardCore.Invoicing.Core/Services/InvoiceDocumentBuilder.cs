using System.Globalization;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Builds the business document of an invoice or credit note: issuer and bill-to as the document keeps them, dates and
/// references, a row per line, the tax breakdown and totals (and for an issued invoice what was paid, credited and is
/// due), notes and terms. Contributors add to it, then the document template of its kind is applied.
/// </summary>
public sealed class InvoiceDocumentBuilder : IInvoiceDocumentBuilder
{
    private const string _description = "description";
    private const string _quantity = "quantity";
    private const string _unitPrice = "unitPrice";
    private const string _tax = "tax";
    private const string _amount = "amount";

    private readonly IMoneyFormatter _moneyFormatter;
    private readonly IBusinessDocumentTemplateManager _templateManager;
    private readonly IEnumerable<IInvoiceDocumentContributor> _contributors;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public InvoiceDocumentBuilder(
        IMoneyFormatter moneyFormatter,
        IBusinessDocumentTemplateManager templateManager,
        IEnumerable<IInvoiceDocumentContributor> contributors,
        IServiceProvider serviceProvider,
        ILogger<InvoiceDocumentBuilder> logger,
        IStringLocalizer<InvoiceDocumentBuilder> stringLocalizer)
    {
        _moneyFormatter = moneyFormatter;
        _templateManager = templateManager;
        _contributors = contributors;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<BusinessDocument> BuildAsync(InvoicingDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var creditNote = document as CreditNote;
        var invoice = document as Invoice;
        var estimate = document as Estimate;
        var issued = document.IssuedUtc.HasValue;
        var currency = document.Currency;
        var culture = string.IsNullOrEmpty(document.Culture) ? null : CultureInfo.GetCultureInfo(document.Culture);

        string Money(decimal amount)
            => string.IsNullOrEmpty(currency) ? amount.ToString("0.00", CultureInfo.InvariantCulture) : _moneyFormatter.Format(amount, currency, culture);

        var business = new BusinessDocument
        {
            Kind = estimate is not null ? InvoicingConstants.BusinessDocumentKinds.Estimate : creditNote is null ? InvoicingConstants.BusinessDocumentKinds.Invoice : InvoicingConstants.BusinessDocumentKinds.CreditNote,
            Title = estimate is not null ? S["Estimate"] : creditNote is not null ? S["Credit note"] : invoice?.Kind == InvoiceKind.DebitNote ? S["Debit note"] : S["Invoice"],
            Number = document.Number,
            Culture = document.Culture,
            Status = GetStatus(document),
            Watermark = !issued ? S["Draft"] : invoice?.Status == InvoiceStatus.Void || creditNote?.Status == CreditNoteStatus.Void ? S["Void"] : estimate?.Status == EstimateStatus.Canceled ? S["Canceled"] : null,
            Issuer = await GetIssuerAsync(document.Issuer),
            LogoUrl = document.Issuer?.LogoUrl,
            Recipient = await GetRecipientAsync(document.BillTo),
            RecipientTitle = estimate is null ? S["Bill to"] : S["Prepared for"],
            Footer = document.Footer,
        };

        if (document.BillTo?.ShippingAddress is { } shipTo)
        {
            business.ShipTo = new BusinessDocumentParty { Name = shipTo.Name ?? shipTo.Company, Lines = [.. await GetLinesAsync(shipTo)] };
            business.ShipToTitle = S["Ship to"];
        }

        // Dates are dates in the site's time zone, kept as dates: printed as they are.
        if (document.IssueDate is { } issueDate)
        {
            business.Fields.Add(new BusinessDocumentField(S["Date"], issueDate.ToString("d", culture ?? CultureInfo.CurrentCulture)));
        }

        if (invoice?.DueDate is { } dueDate)
        {
            business.Fields.Add(new BusinessDocumentField(S["Due"], string.IsNullOrEmpty(invoice.PaymentTermName)
                ? dueDate.ToString("d", culture ?? CultureInfo.CurrentCulture)
                : S["{0} ({1})", dueDate.ToString("d", culture ?? CultureInfo.CurrentCulture), invoice.PaymentTermName]));
        }

        if (creditNote is not null)
        {
            business.Fields.Add(new BusinessDocumentField(S["Credits invoice"], creditNote.InvoiceNumber));
        }

        if (estimate?.ExpiryDate is { } expiryDate)
        {
            business.Fields.Add(new BusinessDocumentField(S["Valid until"], expiryDate.ToString("d", culture ?? CultureInfo.CurrentCulture)));
        }

        if (!string.IsNullOrEmpty(document.PurchaseOrderNumber))
        {
            business.Fields.Add(new BusinessDocumentField(S["Purchase order"], document.PurchaseOrderNumber));
        }

        if (!string.IsNullOrEmpty(document.Reference) && creditNote is null)
        {
            business.Fields.Add(new BusinessDocumentField(S["Reference"], document.Reference));
        }

        business.Fields.Add(new BusinessDocumentField(S["Currency"], currency));

        business.Columns =
        [
            new BusinessDocumentColumn(_description, S["Description"]),
            new BusinessDocumentColumn(_quantity, S["Quantity"], BusinessDocumentAlignment.End, 12),
            new BusinessDocumentColumn(_unitPrice, S["Price each"], BusinessDocumentAlignment.End, 16),
            new BusinessDocumentColumn(_tax, S["Tax"], BusinessDocumentAlignment.End, 14),
            new BusinessDocumentColumn(_amount, S["Amount"], BusinessDocumentAlignment.End, 16),
        ];

        foreach (var line in document.Lines)
        {
            business.Rows.Add(GetRow(line, Money, culture));
        }

        AddTotals(business, document, invoice, Money);

        if (estimate is not null && (estimate.DepositAmount is > 0m || estimate.DepositPercent is > 0m))
        {
            business.Sections.Add(new BusinessDocumentSection(S["Deposit"], estimate.DepositAmount is > 0m
                ? S["A deposit of {0} is due when the estimate is accepted.", Money(estimate.DepositAmount.Value)]
                : S["A deposit of {0}% ({1}) is due when the estimate is accepted.", estimate.DepositPercent.Value.ToString("0.##", culture ?? CultureInfo.CurrentCulture), Money(Math.Round((document.Totals?.Total ?? 0m) * estimate.DepositPercent.Value / 100m, 2, MidpointRounding.AwayFromZero))]));
        }

        if (estimate?.Acceptance is { } acceptance)
        {
            business.Sections.Add(new BusinessDocumentSection(S["Accepted"], S["Accepted by {0}.", acceptance.Name]));
        }

        if (creditNote is not null && !string.IsNullOrWhiteSpace(creditNote.Reason))
        {
            business.Sections.Add(new BusinessDocumentSection(S["Reason"], creditNote.Reason));
        }

        if (!string.IsNullOrWhiteSpace(document.Notes))
        {
            business.Sections.Add(new BusinessDocumentSection(S["Notes"], document.Notes));
        }

        if (!string.IsNullOrWhiteSpace(document.Terms))
        {
            business.Sections.Add(new BusinessDocumentSection(S["Terms"], document.Terms));
        }

        foreach (var contributor in _contributors)
        {
            try
            {
                await contributor.ContributeAsync(document, business, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The invoice document contributor '{Contributor}' failed on '{DocumentId}'.", contributor.GetType().FullName, document.ItemId);
            }
        }

        await _templateManager.ApplyAsync(business, document.TemplateId, cancellationToken);

        return business;
    }

    private BusinessDocumentRow GetRow(DocumentLine line, Func<decimal, string> money, CultureInfo culture)
    {
        if (line.Kind == DocumentLineKind.Section)
        {
            return new BusinessDocumentRow { Kind = BusinessDocumentRowKind.Section, Cells = { [_description] = line.Name } };
        }

        if (!line.HasAmount)
        {
            return new BusinessDocumentRow { Kind = BusinessDocumentRowKind.Note, Cells = { [_description] = line.Name }, Detail = line.Description };
        }

        var detail = line.Description;

        if (line.DiscountPercent is > 0m)
        {
            var discount = S["Less {0}%", line.DiscountPercent.Value.ToString("0.##", culture ?? CultureInfo.CurrentCulture)].Value;
            detail = string.IsNullOrWhiteSpace(detail) ? discount : $"{detail}\n{discount}";
        }

        return new BusinessDocumentRow
        {
            Kind = BusinessDocumentRowKind.Item,
            Cells =
            {
                [_description] = line.Name,
                [_quantity] = string.IsNullOrEmpty(line.Unit)
                    ? line.Quantity.ToString("0.######", culture ?? CultureInfo.CurrentCulture)
                    : $"{line.Quantity.ToString("0.######", culture ?? CultureInfo.CurrentCulture)} {line.Unit}",
                [_unitPrice] = money(line.UnitPrice),
                [_tax] = money(line.TaxAmount),
                [_amount] = money(line.Net),
            },
            Detail = detail,
        };
    }

    private void AddTotals(BusinessDocument business, InvoicingDocument document, Invoice invoice, Func<decimal, string> money)
    {
        var totals = document.Totals ?? new DocumentTotals();

        business.Totals.Add(new BusinessDocumentTotal(S["Subtotal"], money(totals.Subtotal)));

        if (totals.DiscountTotal != 0m)
        {
            business.Totals.Add(new BusinessDocumentTotal(S["Discount"], money(-totals.DiscountTotal)));
        }

        if (totals.ChargeTotal != 0m)
        {
            business.Totals.Add(new BusinessDocumentTotal(S["Charges"], money(totals.ChargeTotal)));
        }

        foreach (var tax in document.Taxes ?? [])
        {
            var name = string.IsNullOrEmpty(tax.Name) ? S["Tax"].Value : tax.Name;

            // Invoicing documents keep a rate in percent, such as 5 for 5%.
            business.Totals.Add(new BusinessDocumentTotal(
                tax.Rate > 0m ? S["{0} ({1}%)", name, tax.Rate.ToString("0.###", CultureInfo.CurrentCulture)].Value : name,
                money(tax.TaxAmount)));
        }

        if (totals.RoundingAmount != 0m)
        {
            business.Totals.Add(new BusinessDocumentTotal(S["Rounding"], money(totals.RoundingAmount)));
        }

        business.Totals.Add(new BusinessDocumentTotal(S["Total"], money(totals.Total), emphasized: invoice is null || !document.IssuedUtc.HasValue));

        if (invoice is null || !invoice.IssuedUtc.HasValue)
        {
            return;
        }

        if (invoice.AmountPaid > 0m)
        {
            business.Totals.Add(new BusinessDocumentTotal(S["Paid"], money(-invoice.AmountPaid), muted: true));
        }

        if (invoice.AmountCredited > 0m)
        {
            business.Totals.Add(new BusinessDocumentTotal(S["Credited"], money(-invoice.AmountCredited), muted: true));
        }

        business.Totals.Add(new BusinessDocumentTotal(S["Amount due"], money(invoice.AmountDue), emphasized: true));
    }

    private BusinessDocumentStatus GetStatus(InvoicingDocument document)
    {
        if (document is Estimate estimate)
        {
            return estimate.Status switch
            {
                EstimateStatus.Sent => new BusinessDocumentStatus(S["Awaiting answer"], BusinessDocumentTone.Info),
                EstimateStatus.Accepted => new BusinessDocumentStatus(S["Accepted"], BusinessDocumentTone.Success),
                EstimateStatus.Invoiced => new BusinessDocumentStatus(S["Invoiced"], BusinessDocumentTone.Success),
                EstimateStatus.Declined => new BusinessDocumentStatus(S["Declined"], BusinessDocumentTone.Danger),
                EstimateStatus.Expired => new BusinessDocumentStatus(S["Expired"], BusinessDocumentTone.Warning),
                EstimateStatus.Canceled => new BusinessDocumentStatus(S["Canceled"], BusinessDocumentTone.Neutral),
                _ => new BusinessDocumentStatus(S["Draft"], BusinessDocumentTone.Neutral),
            };
        }

        if (document is CreditNote creditNote)
        {
            return creditNote.Status switch
            {
                CreditNoteStatus.Issued => new BusinessDocumentStatus(S["Issued"], BusinessDocumentTone.Success),
                CreditNoteStatus.Void => new BusinessDocumentStatus(S["Void"], BusinessDocumentTone.Danger),
                _ => new BusinessDocumentStatus(S["Draft"], BusinessDocumentTone.Neutral),
            };
        }

        var invoice = (Invoice)document;

        return invoice.Status switch
        {
            InvoiceStatus.Draft or InvoiceStatus.PendingApproval => new BusinessDocumentStatus(S["Draft"], BusinessDocumentTone.Neutral),
            InvoiceStatus.Void => new BusinessDocumentStatus(S["Void"], BusinessDocumentTone.Danger),
            InvoiceStatus.Uncollectible => new BusinessDocumentStatus(S["Written off"], BusinessDocumentTone.Danger),
            _ => invoice.Settlement switch
            {
                InvoiceSettlement.Paid => new BusinessDocumentStatus(S["Paid"], BusinessDocumentTone.Success),
                InvoiceSettlement.PartiallyPaid => new BusinessDocumentStatus(S["Partly paid"], BusinessDocumentTone.Info),
                _ => new BusinessDocumentStatus(S["Due"], BusinessDocumentTone.Warning),
            },
        };
    }

    private async Task<BusinessDocumentParty> GetIssuerAsync(DocumentIssuer issuer)
    {
        if (issuer is null)
        {
            return null;
        }

        return new BusinessDocumentParty
        {
            Name = issuer.Name,
            Lines = [.. await GetLinesAsync(issuer.Address)],
            TaxId = issuer.TaxId,
            Email = issuer.Email,
            Phone = issuer.Phone,
            Website = issuer.Website,
        };
    }

    private async Task<BusinessDocumentParty> GetRecipientAsync(DocumentParty billTo)
    {
        if (billTo is null)
        {
            return null;
        }

        var contact = billTo.Contact;

        return new BusinessDocumentParty
        {
            Name = contact?.Name,
            Lines = [.. await GetLinesAsync(contact?.Address)],
            TaxId = billTo.TaxId,
            Email = contact?.Email,
            Phone = contact is { Phone.HasValue: true } ? contact.Phone.ToString() : null,
        };
    }

    // The site's address formatter when the Addresses feature registers one, else the lines as they are kept.
    private async Task<IReadOnlyList<string>> GetLinesAsync(Address address)
    {
        if (address is null)
        {
            return [];
        }

        if (_serviceProvider.GetService<IAddressFormatter>() is { } formatter)
        {
            return await formatter.GetLinesAsync(address);
        }

        return [.. new[] { address.AddressLine1, address.AddressLine2, address.City, address.Region, address.PostalCode, address.Country }
            .Where(part => !string.IsNullOrWhiteSpace(part))];
    }
}
