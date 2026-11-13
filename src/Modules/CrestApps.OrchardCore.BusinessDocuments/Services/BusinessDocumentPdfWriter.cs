using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Pdf;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Lays a business document out as a PDF, written with PdfPig: the title, number and status beside the issuer and its
/// logo, an accent rule, the recipient, ship-to and fields side by side, the line table, the totals on the right, the
/// sections, the footer, page numbers on every page and the watermark behind the content of every page.
/// </summary>
internal sealed class BusinessDocumentPdfWriter
{
    private const double ColumnGap = 16;
    private const double LogoMaximumWidth = 160;
    private const double LogoMaximumHeight = 48;
    private const double BlockSpacing = 14;
    private const double TotalsMinimumWidth = 220;
    private const double TotalPadding = 3;

    private readonly BusinessDocument _document;
    private readonly BusinessDocumentPdfLabels _labels;
    private readonly PdfFonts _fonts;
    private readonly PdfImage _logo;
    private readonly string _accentColor;
    private readonly PdfColor? _accent;

    private PdfFlow _flow;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentPdfWriter"/> class.
    /// </summary>
    /// <param name="document">The document to lay out.</param>
    /// <param name="labels">The localized labels.</param>
    /// <param name="fonts">The font files to write the document in.</param>
    /// <param name="logo">The logo, or <see langword="null"/>.</param>
    public BusinessDocumentPdfWriter(
        BusinessDocument document,
        BusinessDocumentPdfLabels labels,
        PdfFonts fonts,
        PdfImage logo)
    {
        _document = document;
        _labels = labels;
        _fonts = fonts;
        _logo = logo;
        _accentColor = BusinessDocumentStyle.TryNormalizeAccentColor(document.AccentColor, out var color) ? color : null;
        _accent = _accentColor is not null && PdfColor.TryParseHex(_accentColor, out var accent) ? accent : null;
    }

    /// <summary>
    /// Builds the document.
    /// </summary>
    /// <returns>The PDF file.</returns>
    public byte[] Build()
    {
        using var builder = new PdfDocumentBuilder();

        var title = string.Join(" ", new[] { _document.Title, _document.Number }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.DocumentInformation.Title = title;
        }

        var regular = _fonts.CreateRegular(builder);
        var bold = _fonts.CreateBold(builder);
        var (width, height) = BusinessDocumentStyle.GetPaperSize(_document.PaperSize) == BusinessDocumentPaperSize.A4
            ? (BusinessDocumentPdfTheme.A4Width, BusinessDocumentPdfTheme.A4Height)
            : (BusinessDocumentPdfTheme.LetterWidth, BusinessDocumentPdfTheme.LetterHeight);
        var watermark = string.IsNullOrWhiteSpace(_document.Watermark)
            ? null
            : bold.Prepare(_document.Watermark.Trim()).Replace('\n', ' ');

        _flow = new PdfFlow(builder, width, height, regular, bold, watermark is null ? null : page => DrawWatermark(page, watermark, bold, width, height));

        WriteHeader();
        WriteParties();
        WriteLines();
        WriteTotals();
        WriteSections();
        WriteFooter();

        _flow.WriteFooters(_labels.PageNumbers, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);

        return builder.Build();
    }

    // The watermark is drawn when a page starts, so the content of the page is drawn over it.
    private static void DrawWatermark(PdfPage page, string text, PdfFont font, double pageWidth, double pageHeight)
    {
        var unitWidth = font.Measure(text, 1);

        if (unitWidth <= 0)
        {
            return;
        }

        var size = Math.Min(BusinessDocumentPdfTheme.WatermarkMaximumSize, pageWidth * 0.8 / unitWidth);
        var x = (pageWidth - (unitWidth * size)) / 2;
        var top = (pageHeight - (font.LineHeight * size)) / 2;

        page.DrawText(text, font, size, x, top, BusinessDocumentPdfTheme.Watermark);
    }

    private void WriteHeader()
    {
        var leftWidth = (_flow.Width - ColumnGap) * 0.55;
        var rightWidth = _flow.Width - ColumnGap - leftWidth;
        var issuer = new BusinessDocumentPdfBlock(leftWidth);

        if (_logo is not null)
        {
            issuer.AddImage(_logo, LogoMaximumWidth, LogoMaximumHeight).AddSpace(6);
        }

        issuer.AddText(_document.Issuer?.Name, _flow.Bold, BusinessDocumentPdfTheme.NameSize, BusinessDocumentPdfTheme.Text);
        AddPartyDetails(issuer, _document.Issuer, includeContacts: false);

        var heading = new BusinessDocumentPdfBlock(rightWidth)
            .AddText(_document.Title, _flow.Bold, BusinessDocumentPdfTheme.TitleSize, _accent ?? BusinessDocumentPdfTheme.Text, BusinessDocumentAlignment.End)
            .AddText(_document.Number, _flow.Bold, BusinessDocumentPdfTheme.NumberSize, BusinessDocumentPdfTheme.Text, BusinessDocumentAlignment.End);

        if (!string.IsNullOrWhiteSpace(_document.Status?.Label))
        {
            heading.AddSpace(4).AddBadge(
                _document.Status.Label,
                _flow.Bold,
                BusinessDocumentPdfTheme.SmallSize,
                BusinessDocumentPdfTheme.Parse(BusinessDocumentStyle.GetToneBackground(_document.Status.Tone)),
                BusinessDocumentPdfTheme.Parse(BusinessDocumentStyle.GetToneText(_document.Status.Tone)),
                BusinessDocumentAlignment.End);
        }

        var height = Math.Max(issuer.Height, heading.Height);

        if (height > 0)
        {
            _flow.EnsureSpace(height);
            issuer.Draw(_flow.Page, _flow.Left, _flow.Top);
            heading.Draw(_flow.Page, _flow.Left + leftWidth + ColumnGap, _flow.Top);
            _flow.Advance(height + 8);
        }

        _flow.Page.DrawLine(_flow.Left, _flow.Top, _flow.Left + _flow.Width, _flow.Top, _accent ?? BusinessDocumentPdfTheme.Border, 1.5);
        _flow.Advance(BlockSpacing);
    }

    private void WriteParties()
    {
        var parties = new List<(string Title, BusinessDocumentParty Party)>();

        if (HasContent(_document.Recipient))
        {
            parties.Add((_document.RecipientTitle ?? _labels.RecipientTitle, _document.Recipient));
        }

        if (HasContent(_document.ShipTo))
        {
            parties.Add((_document.ShipToTitle ?? _labels.ShipToTitle, _document.ShipTo));
        }

        var fields = (_document.Fields ?? [])
            .Where(field => field is not null && (!string.IsNullOrWhiteSpace(field.Label) || !string.IsNullOrWhiteSpace(field.Value)))
            .ToList();
        var columns = parties.Count + (fields.Count > 0 ? 1 : 0);

        if (columns == 0)
        {
            return;
        }

        var columnWidth = (_flow.Width - (ColumnGap * (columns - 1))) / columns;
        var blocks = new List<BusinessDocumentPdfBlock>();

        foreach (var (title, party) in parties)
        {
            var block = new BusinessDocumentPdfBlock(columnWidth)
                .AddText(title?.ToUpperInvariant(), _flow.Bold, BusinessDocumentPdfTheme.LabelSize, BusinessDocumentPdfTheme.Muted)
                .AddSpace(2)
                .AddText(party.Name, _flow.Bold, BusinessDocumentPdfTheme.BaseSize, BusinessDocumentPdfTheme.Text);

            AddPartyDetails(block, party, includeContacts: true);
            blocks.Add(block);
        }

        var height = blocks.Count == 0 ? 0 : blocks.Max(block => block.Height);
        var fieldRows = CreateFieldRows(fields, columnWidth);

        height = Math.Max(height, fieldRows.Sum(row => Math.Max(row.Label.Height, row.Value.Height)));

        _flow.EnsureSpace(height);

        var left = _flow.Left;

        foreach (var block in blocks)
        {
            block.Draw(_flow.Page, left, _flow.Top);
            left += columnWidth + ColumnGap;
        }

        var top = _flow.Top;

        foreach (var (label, value) in fieldRows)
        {
            label.Draw(_flow.Page, left, top);
            value.Draw(_flow.Page, left + label.Width + 6, top);
            top += Math.Max(label.Height, value.Height);
        }

        _flow.Advance(height + BlockSpacing);
    }

    private List<(BusinessDocumentPdfBlock Label, BusinessDocumentPdfBlock Value)> CreateFieldRows(List<BusinessDocumentField> fields, double width)
    {
        var labelWidth = (width - 6) * 0.45;
        var valueWidth = width - 6 - labelWidth;

        return fields
            .Select(field => (
                new BusinessDocumentPdfBlock(labelWidth).AddText(field.Label, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted).AddSpace(2),
                new BusinessDocumentPdfBlock(valueWidth).AddText(field.Value, _flow.Regular, BusinessDocumentPdfTheme.BaseSize, BusinessDocumentPdfTheme.Text, BusinessDocumentAlignment.End).AddSpace(2)))
            .ToList();
    }

    private void WriteLines()
    {
        var columns = (_document.Columns ?? []).Where(column => column is not null).ToList();
        var rows = (_document.Rows ?? []).Where(row => row is not null).ToList();

        if (columns.Count == 0 || rows.Count == 0)
        {
            return;
        }

        new BusinessDocumentPdfTable(_flow, columns, rows, _accentColor).Write();
        _flow.AddSpace(BlockSpacing);
    }

    private void WriteTotals()
    {
        var totals = (_document.Totals ?? []).Where(total => total is not null).ToList();

        if (totals.Count == 0)
        {
            return;
        }

        var width = Math.Min(_flow.Width, Math.Max(TotalsMinimumWidth, _flow.Width * 0.45));
        var labelWidth = (width - 8) * 0.55;
        var valueWidth = width - 8 - labelWidth;
        var rows = totals
            .Select(total =>
            {
                var font = total.Emphasized ? _flow.Bold : _flow.Regular;
                var size = total.Emphasized ? BusinessDocumentPdfTheme.EmphasizedTotalSize : BusinessDocumentPdfTheme.TotalSize;
                var color = total.Muted && !total.Emphasized ? BusinessDocumentPdfTheme.Muted : BusinessDocumentPdfTheme.Text;
                var label = new BusinessDocumentPdfBlock(labelWidth).AddText(total.Label, font, size, color);
                var value = new BusinessDocumentPdfBlock(valueWidth).AddText(total.Value, font, size, color, BusinessDocumentAlignment.End);

                return (Total: total, Label: label, Value: value, Height: Math.Max(label.Height, value.Height) + (2 * TotalPadding));
            })
            .ToList();
        var height = rows.Sum(row => row.Height);
        var left = _flow.Left + _flow.Width - width;

        _flow.EnsureSpace(height);

        foreach (var row in rows)
        {
            var top = _flow.Top;

            if (row.Total.Emphasized)
            {
                _flow.Page.FillRectangle(left, top, width, row.Height, _accent?.Lighten(0.9) ?? BusinessDocumentPdfTheme.HeaderFill);
                _flow.Page.DrawLine(left, top, left + width, top, _accent ?? BusinessDocumentPdfTheme.Text, 1.25);
            }

            row.Label.Draw(_flow.Page, left + 4, top + TotalPadding);
            row.Value.Draw(_flow.Page, left + 4 + labelWidth, top + TotalPadding);
            _flow.Advance(row.Height);
        }

        _flow.AddSpace(BlockSpacing);
    }

    private void WriteSections()
    {
        foreach (var section in _document.Sections ?? [])
        {
            if (section is null || (string.IsNullOrWhiteSpace(section.Title) && string.IsNullOrWhiteSpace(section.Text)))
            {
                continue;
            }

            List<string> title = string.IsNullOrWhiteSpace(section.Title)
                ? []
                : PdfText.Wrap(_flow.Bold.Prepare(section.Title.Trim()), _flow.Bold, BusinessDocumentPdfTheme.SectionTitleSize, _flow.Width);
            List<string> text = string.IsNullOrWhiteSpace(section.Text)
                ? []
                : PdfText.Wrap(_flow.Regular.Prepare(section.Text.Trim()), _flow.Regular, BusinessDocumentPdfTheme.BaseSize, _flow.Width);

            // The title stays with the first line of its text.
            _flow.EnsureSpace(PdfFlow.GetHeight(title, _flow.Bold, BusinessDocumentPdfTheme.SectionTitleSize) + (text.Count > 0 ? _flow.Regular.LineHeight * BusinessDocumentPdfTheme.BaseSize : 0));
            _flow.WriteLines(title, _flow.Bold, BusinessDocumentPdfTheme.SectionTitleSize, BusinessDocumentPdfTheme.Text);
            _flow.WriteLines(text, _flow.Regular, BusinessDocumentPdfTheme.BaseSize, BusinessDocumentPdfTheme.Text);
            _flow.AddSpace(10);
        }
    }

    private void WriteFooter()
    {
        var contacts = string.Join(" · ", new[] { _document.Issuer?.Email, _document.Issuer?.Phone, _document.Issuer?.Website }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(_document.Footer))
        {
            lines.AddRange(PdfText.Wrap(_flow.Regular.Prepare(_document.Footer.Trim()), _flow.Regular, BusinessDocumentPdfTheme.SmallSize, _flow.Width));
        }

        if (!string.IsNullOrEmpty(contacts))
        {
            lines.AddRange(PdfText.Wrap(_flow.Regular.Prepare(contacts), _flow.Regular, BusinessDocumentPdfTheme.SmallSize, _flow.Width));
        }

        if (lines.Count == 0)
        {
            return;
        }

        _flow.EnsureSpace(8 + (_flow.Regular.LineHeight * BusinessDocumentPdfTheme.SmallSize));
        _flow.Page.DrawLine(_flow.Left, _flow.Top, _flow.Left + _flow.Width, _flow.Top, BusinessDocumentPdfTheme.Border, 0.5);
        _flow.Advance(6);
        _flow.WriteLines(lines, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);
    }

    private void AddPartyDetails(BusinessDocumentPdfBlock block, BusinessDocumentParty party, bool includeContacts)
    {
        if (party is null)
        {
            return;
        }

        foreach (var line in party.Lines ?? [])
        {
            block.AddText(line, _flow.Regular, BusinessDocumentPdfTheme.BaseSize, includeContacts ? BusinessDocumentPdfTheme.Text : BusinessDocumentPdfTheme.Muted);
        }

        if (!string.IsNullOrWhiteSpace(party.TaxId))
        {
            block.AddText(_labels.TaxId.Replace("{0}", party.TaxId.Trim(), StringComparison.Ordinal), _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);
        }

        if (includeContacts)
        {
            foreach (var contact in new[] { party.Email, party.Phone, party.Website })
            {
                block.AddText(contact, _flow.Regular, BusinessDocumentPdfTheme.SmallSize, BusinessDocumentPdfTheme.Muted);
            }
        }
    }

    private static bool HasContent(BusinessDocumentParty party)
        => party is not null &&
            (!string.IsNullOrWhiteSpace(party.Name) ||
            (party.Lines?.Any(line => !string.IsNullOrWhiteSpace(line)) ?? false) ||
            !string.IsNullOrWhiteSpace(party.TaxId) ||
            !string.IsNullOrWhiteSpace(party.Email) ||
            !string.IsNullOrWhiteSpace(party.Phone) ||
            !string.IsNullOrWhiteSpace(party.Website));
}
