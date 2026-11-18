using CrestApps.OrchardCore.Pdf;
using CrestApps.OrchardCore.Pdf.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.Models;
using CrestApps.OrchardCore.Skills.IssuedCertificates.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Drivers;

/// <summary>
/// The editor of a certificate template: page, colors, font, QR code and the Liquid text blocks. Three empty rows are
/// always offered for new blocks; an empty row is dropped on save.
/// </summary>
internal sealed class CertificateTemplatePartDisplayDriver : ContentPartDisplayDriver<CertificateTemplatePart>
{
    private const int EmptyRows = 3;

    internal readonly IStringLocalizer S;

    public CertificateTemplatePartDisplayDriver(IStringLocalizer<CertificateTemplatePartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(CertificateTemplatePart part, BuildPartEditorContext context)
    {
        return Initialize<CertificateTemplatePartViewModel>(GetEditorShapeType(context), model =>
        {
            var blocks = part.Blocks.Count == 0 && context.IsNew ? CertificateTemplateDefaults.CreateBlocks() : part.Blocks;

            model.PageSize = part.PageSize;
            model.Landscape = part.Landscape;
            model.FontFamily = part.FontFamily;
            model.AccentColor = part.AccentColor;
            model.TextColor = part.TextColor;
            model.ShowBorder = part.ShowBorder;
            model.ShowQrCode = part.ShowQrCode;
            model.QrX = part.QrX;
            model.QrTop = part.QrTop;
            model.QrSize = part.QrSize;
            model.Blocks = [.. blocks, .. Enumerable.Range(0, EmptyRows).Select(_ => new CertificateTemplateBlock())];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(CertificateTemplatePart part, UpdatePartEditorContext context)
    {
        var model = new CertificateTemplatePartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.PageSize = string.Equals(model.PageSize, "A4", StringComparison.OrdinalIgnoreCase) ? "A4" : "Letter";
        part.Landscape = model.Landscape;
        part.FontFamily = string.IsNullOrWhiteSpace(model.FontFamily) ? null : model.FontFamily.Trim();
        part.AccentColor = ValidColor(model.AccentColor, nameof(model.AccentColor), context) ?? part.AccentColor;
        part.TextColor = ValidColor(model.TextColor, nameof(model.TextColor), context) ?? part.TextColor;
        part.ShowBorder = model.ShowBorder;
        part.ShowQrCode = model.ShowQrCode;
        part.QrX = Math.Max(0, model.QrX);
        part.QrTop = Math.Max(0, model.QrTop);
        part.QrSize = Math.Clamp(model.QrSize, 36, 300);
        part.Blocks = (model.Blocks ?? [])
            .Where(block => !string.IsNullOrWhiteSpace(block.Text))
            .Select(block => new CertificateTemplateBlock
            {
                Text = block.Text.Trim(),
                X = Math.Max(0, block.X),
                Top = Math.Max(0, block.Top),
                Width = Math.Max(0, block.Width),
                FontSize = Math.Clamp(block.FontSize, 4, 120),
                Bold = block.Bold,
                Alignment = Enum.IsDefined(block.Alignment) ? block.Alignment : PdfTextAlignment.Center,
                Accent = block.Accent,
                MaxLines = Math.Max(0, block.MaxLines),
            })
            .ToList();

        return Edit(part, context);
    }

    private string ValidColor(string value, string name, UpdatePartEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!PdfColor.TryParse(value, out _))
        {
            context.Updater.ModelState.AddModelError(Prefix, name, S["'{0}' is not a color such as #1E3A8A.", value]);

            return null;
        }

        return value.Trim();
    }
}
