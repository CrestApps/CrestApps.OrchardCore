using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.BusinessDocuments.Core.Services;

/// <summary>
/// The default <see cref="IBusinessDocumentHtmlRenderer"/>. It lays the document out with tables and inline styles,
/// which is what mail clients render reliably, and HTML-encodes every value it takes from the document. The accent color
/// and the logo address are used only when they pass <see cref="BusinessDocumentStyle"/>'s checks.
/// </summary>
public sealed class BusinessDocumentHtmlRenderer : IBusinessDocumentHtmlRenderer
{
    private const string FontFamily = "Arial,Helvetica,sans-serif";

    private readonly HtmlEncoder _encoder;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentHtmlRenderer"/> class.
    /// </summary>
    /// <param name="encoder">The HTML encoder applied to every value from the document.</param>
    /// <param name="stringLocalizer">The localizer of the default labels.</param>
    public BusinessDocumentHtmlRenderer(
        HtmlEncoder encoder,
        IStringLocalizer<BusinessDocumentHtmlRenderer> stringLocalizer)
    {
        _encoder = encoder;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task<string> RenderAsync(BusinessDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var culture = BusinessDocumentCulture.Use(document.Culture);

        return Task.FromResult(Render(document));
    }

    private string Render(BusinessDocument document)
    {
        var accent = BusinessDocumentStyle.TryNormalizeAccentColor(document.AccentColor, out var color) ? color : null;
        var html = new StringBuilder();

        html.Append("<div style=\"font-family:").Append(FontFamily).Append(";color:").Append(BusinessDocumentStyle.TextColor)
            .Append(";font-size:14px;line-height:1.4;max-width:640px;\">");

        AppendWatermark(html, document.Watermark);
        AppendHeader(html, document, accent);
        AppendParties(html, document);
        AppendLines(html, document, accent);
        AppendTotals(html, document, accent);
        AppendSections(html, document);
        AppendFooter(html, document);

        html.Append("</div>");

        return html.ToString();
    }

    // A mail client cannot lay text over the document, so the watermark is a banner at the top.
    private void AppendWatermark(StringBuilder html, string watermark)
    {
        if (string.IsNullOrWhiteSpace(watermark))
        {
            return;
        }

        html.Append("<div style=\"display:inline-block;border:2px solid #D1D5DB;color:#9CA3AF;font-weight:bold;letter-spacing:2px;text-transform:uppercase;padding:2px 10px;margin-bottom:12px;\">")
            .Append(Encode(watermark))
            .Append("</div>");
    }

    private void AppendHeader(StringBuilder html, BusinessDocument document, string accent)
    {
        html.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin-bottom:16px;\"><tr>");
        html.Append("<td style=\"vertical-align:top;padding:0;\">");

        if (BusinessDocumentStyle.IsSafeImageUrl(document.LogoUrl))
        {
            html.Append("<img src=\"").Append(Encode(document.LogoUrl.Trim())).Append("\" alt=\"")
                .Append(Encode(document.Issuer?.Name))
                .Append("\" style=\"max-height:64px;max-width:200px;margin-bottom:8px;display:block;\" />");
        }

        var issuer = document.Issuer;

        if (!string.IsNullOrWhiteSpace(issuer?.Name))
        {
            html.Append("<div style=\"font-size:18px;font-weight:bold;\">").Append(Encode(issuer.Name)).Append("</div>");
        }

        AppendPartyDetails(html, issuer, includeContacts: false);
        html.Append("</td>");

        html.Append("<td style=\"vertical-align:top;text-align:right;padding:0;\">");

        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            html.Append("<div style=\"font-size:22px;font-weight:bold;color:").Append(accent ?? BusinessDocumentStyle.TextColor).Append(";\">")
                .Append(Encode(document.Title))
                .Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(document.Number))
        {
            html.Append("<div style=\"font-size:15px;font-weight:bold;\">").Append(Encode(document.Number)).Append("</div>");
        }

        if (!string.IsNullOrWhiteSpace(document.Status?.Label))
        {
            html.Append("<div style=\"display:inline-block;margin-top:6px;padding:2px 8px;border-radius:4px;font-size:12px;font-weight:bold;background:")
                .Append(BusinessDocumentStyle.GetToneBackground(document.Status.Tone))
                .Append(";color:")
                .Append(BusinessDocumentStyle.GetToneText(document.Status.Tone))
                .Append(";\">")
                .Append(Encode(document.Status.Label))
                .Append("</div>");
        }

        html.Append("</td></tr></table>");
    }

    private void AppendParties(StringBuilder html, BusinessDocument document)
    {
        var fields = (document.Fields ?? []).Where(field => field is not null && (!string.IsNullOrWhiteSpace(field.Label) || !string.IsNullOrWhiteSpace(field.Value))).ToList();
        var hasRecipient = HasContent(document.Recipient);
        var hasShipTo = HasContent(document.ShipTo);

        if (!hasRecipient && !hasShipTo && fields.Count == 0)
        {
            return;
        }

        html.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin-bottom:16px;\"><tr>");

        if (hasRecipient)
        {
            AppendPartyCell(html, document.RecipientTitle ?? S["Bill to"].Value, document.Recipient);
        }

        if (hasShipTo)
        {
            AppendPartyCell(html, document.ShipToTitle ?? S["Ship to"].Value, document.ShipTo);
        }

        if (fields.Count > 0)
        {
            html.Append("<td style=\"vertical-align:top;padding:0;\"><table role=\"presentation\" style=\"border-collapse:collapse;margin-left:auto;\">");

            foreach (var field in fields)
            {
                html.Append("<tr><td style=\"padding:1px 12px 1px 0;color:").Append(BusinessDocumentStyle.MutedColor).Append(";\">")
                    .Append(Encode(field.Label))
                    .Append("</td><td style=\"padding:1px 0;text-align:right;\">")
                    .Append(Encode(field.Value))
                    .Append("</td></tr>");
            }

            html.Append("</table></td>");
        }

        html.Append("</tr></table>");
    }

    private void AppendPartyCell(StringBuilder html, string title, BusinessDocumentParty party)
    {
        html.Append("<td style=\"vertical-align:top;padding:0 16px 0 0;\">");
        html.Append("<div style=\"font-size:12px;text-transform:uppercase;color:").Append(BusinessDocumentStyle.MutedColor).Append(";margin-bottom:2px;\">")
            .Append(Encode(title))
            .Append("</div>");

        if (!string.IsNullOrWhiteSpace(party.Name))
        {
            html.Append("<div style=\"font-weight:bold;\">").Append(Encode(party.Name)).Append("</div>");
        }

        AppendPartyDetails(html, party, includeContacts: true);
        html.Append("</td>");
    }

    private void AppendPartyDetails(StringBuilder html, BusinessDocumentParty party, bool includeContacts)
    {
        if (party is null)
        {
            return;
        }

        var lines = (party.Lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        if (!string.IsNullOrWhiteSpace(party.TaxId))
        {
            lines.Add(S["Tax ID: {0}", party.TaxId].Value);
        }

        if (includeContacts)
        {
            lines.AddRange(new[] { party.Email, party.Phone, party.Website }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        foreach (var line in lines)
        {
            html.Append("<div style=\"font-size:13px;color:").Append(BusinessDocumentStyle.MutedColor).Append(";\">").Append(Encode(line)).Append("</div>");
        }
    }

    private void AppendLines(StringBuilder html, BusinessDocument document, string accent)
    {
        var columns = (document.Columns ?? []).Where(column => column is not null).ToList();
        var rows = (document.Rows ?? []).Where(row => row is not null).ToList();

        if (columns.Count == 0 || rows.Count == 0)
        {
            return;
        }

        var weights = columns.Sum(column => column.Width > 0 ? column.Width : 0);
        var headerText = accent is null || BusinessDocumentStyle.PrefersDarkText(accent) ? BusinessDocumentStyle.TextColor : "#FFFFFF";

        html.Append("<table style=\"width:100%;border-collapse:collapse;margin-bottom:12px;\"><thead><tr>");

        foreach (var column in columns)
        {
            html.Append("<th style=\"padding:6px 8px;text-align:").Append(Align(column.Alignment))
                .Append(";background:").Append(accent ?? BusinessDocumentStyle.HeaderFill)
                .Append(";color:").Append(headerText).Append(';');

            if (column.Width > 0 && weights > 0)
            {
                html.Append("width:").Append(Math.Round(column.Width / weights * 100).ToString(CultureInfo.InvariantCulture)).Append("%;");
            }

            html.Append("\">").Append(Encode(column.Title)).Append("</th>");
        }

        html.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            AppendRow(html, row, columns);
        }

        html.Append("</tbody></table>");
    }

    private void AppendRow(StringBuilder html, BusinessDocumentRow row, List<BusinessDocumentColumn> columns)
    {
        var border = "border-bottom:1px solid " + BusinessDocumentStyle.BorderColor + ";";

        if (row.Kind is BusinessDocumentRowKind.Section or BusinessDocumentRowKind.Note)
        {
            var text = GetSpanningText(row, columns);
            var style = row.Kind == BusinessDocumentRowKind.Section
                ? "font-weight:bold;background:#F9FAFB;"
                : "font-size:12px;color:" + BusinessDocumentStyle.MutedColor + ";";

            html.Append("<tr><td colspan=\"").Append(columns.Count).Append("\" style=\"padding:6px 8px;").Append(border).Append(style).Append("\">")
                .Append(Encode(text));

            AppendDetail(html, row.Detail);
            html.Append("</td></tr>");

            return;
        }

        var subtotal = row.Kind == BusinessDocumentRowKind.Subtotal;

        html.Append("<tr>");

        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];

            html.Append("<td style=\"padding:6px 8px;vertical-align:top;text-align:").Append(Align(column.Alignment)).Append(';').Append(border);

            if (subtotal)
            {
                html.Append("font-weight:bold;border-top:1px solid #9CA3AF;");
            }

            html.Append("\">").Append(Encode(GetCell(row, column)));

            if (index == 0)
            {
                AppendDetail(html, row.Detail);
            }

            html.Append("</td>");
        }

        html.Append("</tr>");
    }

    private void AppendDetail(StringBuilder html, string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        html.Append("<div style=\"font-size:12px;font-weight:normal;color:").Append(BusinessDocumentStyle.MutedColor).Append(";white-space:pre-line;\">")
            .Append(Encode(detail))
            .Append("</div>");
    }

    private void AppendTotals(StringBuilder html, BusinessDocument document, string accent)
    {
        var totals = (document.Totals ?? []).Where(total => total is not null).ToList();

        if (totals.Count == 0)
        {
            return;
        }

        html.Append("<table role=\"presentation\" style=\"border-collapse:collapse;margin:0 0 16px auto;min-width:260px;\">");

        foreach (var total in totals)
        {
            var style = total.Emphasized
                ? "font-weight:bold;font-size:16px;border-top:2px solid " + (accent ?? BusinessDocumentStyle.TextColor) + ";"
                : total.Muted
                    ? "color:" + BusinessDocumentStyle.MutedColor + ";"
                    : string.Empty;

            html.Append("<tr><td style=\"padding:4px 16px 4px 0;").Append(style).Append("\">")
                .Append(Encode(total.Label))
                .Append("</td><td style=\"padding:4px 0;text-align:right;").Append(style).Append("\">")
                .Append(Encode(total.Value))
                .Append("</td></tr>");
        }

        html.Append("</table>");
    }

    private void AppendSections(StringBuilder html, BusinessDocument document)
    {
        foreach (var section in document.Sections ?? [])
        {
            if (section is null || (string.IsNullOrWhiteSpace(section.Title) && string.IsNullOrWhiteSpace(section.Text)))
            {
                continue;
            }

            html.Append("<div style=\"margin-bottom:12px;\">");

            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                html.Append("<div style=\"font-weight:bold;margin-bottom:2px;\">").Append(Encode(section.Title)).Append("</div>");
            }

            if (!string.IsNullOrWhiteSpace(section.Text))
            {
                html.Append("<div style=\"font-size:13px;white-space:pre-line;\">").Append(Encode(section.Text)).Append("</div>");
            }

            html.Append("</div>");
        }
    }

    private void AppendFooter(StringBuilder html, BusinessDocument document)
    {
        var contacts = string.Join(" · ", new[] { document.Issuer?.Email, document.Issuer?.Phone, document.Issuer?.Website }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        if (string.IsNullOrWhiteSpace(document.Footer) && string.IsNullOrEmpty(contacts))
        {
            return;
        }

        html.Append("<div style=\"border-top:1px solid ").Append(BusinessDocumentStyle.BorderColor).Append(";padding-top:8px;font-size:12px;color:")
            .Append(BusinessDocumentStyle.MutedColor).Append(";\">");

        if (!string.IsNullOrWhiteSpace(document.Footer))
        {
            html.Append("<div style=\"white-space:pre-line;\">").Append(Encode(document.Footer)).Append("</div>");
        }

        if (!string.IsNullOrEmpty(contacts))
        {
            html.Append("<div>").Append(Encode(contacts)).Append("</div>");
        }

        html.Append("</div>");
    }

    private string Encode(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : _encoder.Encode(value);

    private static bool HasContent(BusinessDocumentParty party)
        => party is not null &&
            (!string.IsNullOrWhiteSpace(party.Name) ||
            (party.Lines?.Any(line => !string.IsNullOrWhiteSpace(line)) ?? false) ||
            !string.IsNullOrWhiteSpace(party.TaxId) ||
            !string.IsNullOrWhiteSpace(party.Email) ||
            !string.IsNullOrWhiteSpace(party.Phone) ||
            !string.IsNullOrWhiteSpace(party.Website));

    private static string GetCell(BusinessDocumentRow row, BusinessDocumentColumn column)
        => column.Name is not null && row.Cells is not null && row.Cells.TryGetValue(column.Name, out var value) ? value : null;

    private static string GetSpanningText(BusinessDocumentRow row, List<BusinessDocumentColumn> columns)
        => GetCell(row, columns[0]) ?? row.Cells?.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string Align(BusinessDocumentAlignment alignment)
        => alignment switch
        {
            BusinessDocumentAlignment.Center => "center",
            BusinessDocumentAlignment.End => "right",
            _ => "left",
        };
}
