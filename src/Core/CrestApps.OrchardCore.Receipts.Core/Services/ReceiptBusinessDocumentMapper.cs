using System.Globalization;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Receipts.Core.Services;

/// <summary>
/// Turns a <see cref="ReceiptDocument"/> into the <see cref="BusinessDocument"/> the shared renderers print, of the
/// <see cref="ReceiptsConstants.BusinessDocumentKind"/> kind: the receipt's amounts are written with the shared money
/// format, and its labels are localized here, so the renderers only lay it out.
/// </summary>
public sealed class ReceiptBusinessDocumentMapper
{
    private const string DescriptionColumn = "Description";
    private const string QuantityColumn = "Quantity";
    private const string AmountColumn = "Amount";

    private readonly IMoneyFormatter _moneyFormatter;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiptBusinessDocumentMapper"/> class.
    /// </summary>
    /// <param name="moneyFormatter">The formatter the amounts are written with.</param>
    /// <param name="stringLocalizer">The localizer of the receipt's labels.</param>
    public ReceiptBusinessDocumentMapper(
        IMoneyFormatter moneyFormatter,
        IStringLocalizer<ReceiptBusinessDocumentMapper> stringLocalizer)
    {
        _moneyFormatter = moneyFormatter;
        S = stringLocalizer;
    }

    /// <summary>
    /// Maps a receipt to a business document.
    /// </summary>
    /// <param name="receipt">The receipt.</param>
    /// <returns>The business document.</returns>
    public BusinessDocument Map(ReceiptDocument receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var document = new BusinessDocument
        {
            Kind = ReceiptsConstants.BusinessDocumentKind,
            Title = string.IsNullOrWhiteSpace(receipt.HeaderTitle) ? S["Payment receipt"].Value : receipt.HeaderTitle,
            Status = GetStatus(receipt.Status),
            Watermark = receipt.IsTest && receipt.ShowTestBadge ? S["Test payment"].Value : null,
            Issuer = new BusinessDocumentParty
            {
                Name = receipt.BusinessName,
                Lines = SplitLines(receipt.BusinessAddress),
                Email = receipt.ContactEmail,
                Phone = receipt.ContactPhone,
                Website = receipt.Website,
            },
            Recipient = new BusinessDocumentParty
            {
                Name = receipt.BilledToName,
                Email = receipt.BilledToEmail,
            },
            RecipientTitle = S["Billed to"].Value,
            LogoUrl = receipt.LogoUrl,
            Footer = receipt.FooterText,
            Columns =
            [
                new BusinessDocumentColumn(DescriptionColumn, S["Description"].Value),
                new BusinessDocumentColumn(QuantityColumn, S["Qty"].Value, BusinessDocumentAlignment.End),
                new BusinessDocumentColumn(AmountColumn, S["Amount"].Value, BusinessDocumentAlignment.End),
            ],
        };

        if (!string.IsNullOrWhiteSpace(receipt.Reference))
        {
            document.Fields.Add(new BusinessDocumentField(string.IsNullOrWhiteSpace(receipt.SourceLabel) ? S["Reference"].Value : receipt.SourceLabel, receipt.Reference));
        }

        // The receipt's date is already in the time zone its consumer chose.
        document.Fields.Add(new BusinessDocumentField(S["Date"].Value, receipt.IssuedAt.ToString("g", CultureInfo.CurrentCulture)));

        // Amounts print with a symbol, which several currencies share (the US and Canadian dollars both print "$"), so
        // the receipt names its currency by its code.
        if (!string.IsNullOrWhiteSpace(receipt.Currency))
        {
            document.Fields.Add(new BusinessDocumentField(S["Currency"].Value, receipt.Currency.ToUpperInvariant()));
        }

        foreach (var lineItem in receipt.LineItems ?? [])
        {
            if (lineItem is null)
            {
                continue;
            }

            document.Rows.Add(new BusinessDocumentRow
            {
                Cells =
                {
                    [DescriptionColumn] = lineItem.Description,
                    [QuantityColumn] = lineItem.Quantity.ToString("N0", CultureInfo.CurrentCulture),
                    [AmountColumn] = Money(lineItem.Amount, receipt.Currency),
                },
            });
        }

        AddTotals(document, receipt);

        if (!string.IsNullOrWhiteSpace(receipt.Notes))
        {
            document.Sections.Add(new BusinessDocumentSection(S["Notes"].Value, receipt.Notes));
        }

        return document;
    }

    private void AddTotals(BusinessDocument document, ReceiptDocument receipt)
    {
        var taxLines = (receipt.TaxLines ?? []).Where(line => line is not null).ToList();

        if (receipt.TaxAmount != 0m || taxLines.Count > 0)
        {
            document.Totals.Add(new BusinessDocumentTotal(S["Subtotal"].Value, Money(receipt.Subtotal, receipt.Currency)));

            foreach (var taxLine in taxLines)
            {
                document.Totals.Add(new BusinessDocumentTotal(string.IsNullOrWhiteSpace(taxLine.Description) ? S["Tax"].Value : taxLine.Description, Money(taxLine.Amount, receipt.Currency), muted: true));
            }

            if (taxLines.Count == 0)
            {
                document.Totals.Add(new BusinessDocumentTotal(S["Tax"].Value, Money(receipt.TaxAmount, receipt.Currency), muted: true));
            }
        }

        var totalLabel = receipt.Status == ReceiptStatus.Paid ? S["Total paid"].Value : S["Total"].Value;

        document.Totals.Add(new BusinessDocumentTotal(totalLabel, Money(receipt.Total, receipt.Currency), emphasized: true));
    }

    private BusinessDocumentStatus GetStatus(ReceiptStatus status)
        => status switch
        {
            ReceiptStatus.Paid => new BusinessDocumentStatus(S["Paid"].Value, BusinessDocumentTone.Success),
            ReceiptStatus.Failed => new BusinessDocumentStatus(S["Failed"].Value, BusinessDocumentTone.Danger),
            ReceiptStatus.Due => new BusinessDocumentStatus(S["Due"].Value, BusinessDocumentTone.Warning),
            _ => new BusinessDocumentStatus(S["Pending"].Value, BusinessDocumentTone.Neutral),
        };

    private string Money(decimal amount, string currency)
        => _moneyFormatter.Format(amount, currency);

    private static List<string> SplitLines(string value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
