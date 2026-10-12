using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Receipts.Services;

/// <summary>
/// The default <see cref="IReceiptHtmlRenderer"/>. It lays the receipt out as a single table with inline styles,
/// which is what mail clients render reliably.
/// </summary>
public sealed class DefaultReceiptHtmlRenderer : IReceiptHtmlRenderer
{
    private const string _cell = "padding:6px 0;border-bottom:1px solid #e5e7eb;";

    private readonly HtmlEncoder _encoder;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultReceiptHtmlRenderer"/> class.
    /// </summary>
    /// <param name="encoder">The HTML encoder applied to every value from the receipt.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultReceiptHtmlRenderer(
        HtmlEncoder encoder,
        IStringLocalizer<DefaultReceiptHtmlRenderer> stringLocalizer)
    {
        _encoder = encoder;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Render(ReceiptDocument receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var culture = CultureInfo.CurrentCulture;
        var html = new StringBuilder();

        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#111827;max-width:560px;\">");

        var title = string.IsNullOrWhiteSpace(receipt.HeaderTitle) ? S["Payment receipt"].Value : receipt.HeaderTitle;

        if (!string.IsNullOrWhiteSpace(receipt.BusinessName))
        {
            html.Append("<div style=\"font-size:18px;font-weight:bold;\">").Append(Encode(receipt.BusinessName)).Append("</div>");
        }

        html.Append("<div style=\"color:#6b7280;margin-bottom:16px;\">").Append(Encode(title)).Append("</div>");

        if (receipt.IsTest && receipt.ShowTestBadge)
        {
            html.Append("<div style=\"display:inline-block;background:#fde68a;padding:2px 8px;border-radius:4px;margin-bottom:12px;\">")
                .Append(Encode(S["Test payment"].Value))
                .Append("</div>");
        }

        html.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px;\">");

        if (!string.IsNullOrWhiteSpace(receipt.Reference))
        {
            var label = string.IsNullOrWhiteSpace(receipt.SourceLabel) ? S["Reference"].Value : receipt.SourceLabel;

            AppendRow(html, label, receipt.Reference);
        }

        AppendRow(html, S["Date"].Value, receipt.IssuedAt.ToString("g", culture));

        if (!string.IsNullOrWhiteSpace(receipt.BilledToName) || !string.IsNullOrWhiteSpace(receipt.BilledToEmail))
        {
            AppendRow(html, S["Billed to"].Value, string.Join(" — ", new[] { receipt.BilledToName, receipt.BilledToEmail }.Where(value => !string.IsNullOrWhiteSpace(value))));
        }

        foreach (var lineItem in receipt.LineItems ?? [])
        {
            var description = lineItem.Quantity > 1
                ? $"{lineItem.Description} × {lineItem.Quantity}"
                : lineItem.Description;

            AppendRow(html, description, Money(lineItem.Amount, receipt.Currency));
        }

        if (receipt.TaxAmount > 0m)
        {
            AppendRow(html, S["Subtotal"].Value, Money(receipt.Subtotal, receipt.Currency));

            foreach (var taxLine in receipt.TaxLines ?? [])
            {
                AppendRow(html, taxLine.Description, Money(taxLine.Amount, receipt.Currency));
            }

            if (receipt.TaxLines is null || receipt.TaxLines.Count == 0)
            {
                AppendRow(html, S["Tax"].Value, Money(receipt.TaxAmount, receipt.Currency));
            }
        }

        html.Append("<tr><td style=\"padding:10px 0;font-weight:bold;\">")
            .Append(Encode(S["Total paid"].Value))
            .Append("</td><td style=\"padding:10px 0;font-weight:bold;text-align:right;\">")
            .Append(Encode(Money(receipt.Total, receipt.Currency)))
            .Append("</td></tr>");

        html.Append("</table>");

        if (!string.IsNullOrWhiteSpace(receipt.Notes))
        {
            html.Append("<p style=\"color:#6b7280;font-size:13px;\">").Append(Encode(receipt.Notes)).Append("</p>");
        }

        var contact = string.Join(" · ", new[] { receipt.BusinessAddress, receipt.ContactEmail, receipt.ContactPhone, receipt.Website }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrEmpty(contact))
        {
            html.Append("<p style=\"color:#6b7280;font-size:12px;\">").Append(Encode(contact)).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(receipt.FooterText))
        {
            html.Append("<p style=\"color:#6b7280;font-size:12px;\">").Append(Encode(receipt.FooterText)).Append("</p>");
        }

        html.Append("</div>");

        return html.ToString();
    }

    private void AppendRow(StringBuilder html, string label, string value)
        => html.Append("<tr><td style=\"").Append(_cell).Append("\">")
            .Append(Encode(label))
            .Append("</td><td style=\"").Append(_cell).Append("text-align:right;\">")
            .Append(Encode(value))
            .Append("</td></tr>");

    private string Encode(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : _encoder.Encode(value);

    private static string Money(decimal amount, string currency)
    {
        var formatted = amount.ToString("N" + CurrencyScale.GetDecimalPlaces(currency).ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);

        return string.IsNullOrEmpty(currency)
            ? formatted
            : $"{currency} {formatted}";
    }
}
