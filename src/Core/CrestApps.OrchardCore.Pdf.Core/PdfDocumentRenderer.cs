using CrestApps.OrchardCore.Pdf.Models;
using UglyToad.PdfPig.Writer;

namespace CrestApps.OrchardCore.Pdf;

/// <summary>
/// Renders a <see cref="PdfDocumentLayout"/> with PdfPig.
/// </summary>
/// <remarks>
/// The text is written in the font files of the layout when PdfPig can embed them, otherwise in the installed sans-serif
/// font (the faces of <see cref="DefaultFontFamily"/>, or of the font that stands in for it), or in Helvetica.
/// </remarks>
public sealed class PdfDocumentRenderer : IPdfDocumentRenderer
{
    /// <summary>
    /// The installed font family a layout without font files is written in.
    /// </summary>
    public const string DefaultFontFamily = "Arial";

    /// <inheritdoc/>
    public byte[] Render(PdfDocumentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var fonts = SelectFonts(layout);

        using var builder = new PdfDocumentBuilder();

        builder.DocumentInformation.Title = layout.Title;
        builder.DocumentInformation.Author = layout.Author;

        var regular = fonts.CreateRegular(builder);
        var bold = fonts.CreateBold(builder);
        var pages = layout.Pages.Count > 0 ? layout.Pages : [new PdfPageLayout()];

        foreach (var page in pages)
        {
            var width = page.Size.Width;
            var height = page.Size.Height;
            var canvas = new PdfCanvas(builder.AddPage(width, height), height);

            if (page.BackgroundColor is { } background)
            {
                canvas.FillRectangle(0, 0, width, height, background);
            }

            canvas.DrawImage(page.BackgroundImage, 0, 0, width, height);

            foreach (var element in page.Elements)
            {
                switch (element)
                {
                    case PdfTextElement text:
                        DrawText(canvas, text, text.Bold ? bold : regular);
                        break;

                    case PdfImageElement image:
                        canvas.DrawImage(image.Data, image.X, image.Top, image.Width, image.Height);
                        break;

                    case PdfLineElement line:
                        canvas.DrawLine(line.X, line.Top, line.X2, line.Top2, line.Color, line.Thickness);
                        break;

                    case PdfRectangleElement rectangle:
                        DrawRectangle(canvas, rectangle);
                        break;

                    case PdfQrCodeElement qrCode:
                        PdfQrCode.Draw(canvas, qrCode.Value, qrCode.X, qrCode.Top, qrCode.Size, qrCode.Color);
                        break;
                }
            }
        }

        return builder.Build();
    }

    private static PdfFontSet SelectFonts(PdfDocumentLayout layout)
    {
        if (layout.RegularFont is null)
        {
            return PdfFontSet.GetInstalled(DefaultFontFamily);
        }

        var fonts = new PdfFontSet(layout.RegularFont, layout.BoldFont);

        // A file PdfPig cannot embed is not used; the installed font writes the document instead of Helvetica.
        return fonts.Regular is null ? PdfFontSet.GetInstalled(DefaultFontFamily) : fonts;
    }

    private static void DrawText(PdfCanvas canvas, PdfTextElement element, PdfFontFace font)
    {
        var size = element.FontSize > 0 ? element.FontSize : 12;
        var prepared = font.Prepare(element.Text);

        if (prepared.Length == 0)
        {
            return;
        }

        var lines = element.Width > 0
            ? PdfText.Wrap(prepared, font, size, element.Width)
            : [.. prepared.Split('\n')];

        if (element.MaxLines > 0 && lines.Count > element.MaxLines)
        {
            var last = element.MaxLines - 1;
            var rest = string.Join(' ', lines.Skip(last));
            var width = element.Width > 0 ? element.Width : font.Measure(lines[last], size);

            lines = [.. lines.Take(last), PdfText.Fit(rest, font, size, width)];
        }

        var lineHeight = font.LineHeight * size;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var measure = font.Measure(line, size);
            var x = element.Alignment switch
            {
                PdfTextAlignment.Center when element.Width > 0 => element.X + ((element.Width - measure) / 2),
                PdfTextAlignment.Right when element.Width > 0 => element.X + element.Width - measure,
                PdfTextAlignment.Center => element.X - (measure / 2),
                PdfTextAlignment.Right => element.X - measure,
                _ => element.X,
            };

            canvas.DrawText(line, font, size, x, element.Top + (index * lineHeight), element.Color);
        }
    }

    private static void DrawRectangle(PdfCanvas canvas, PdfRectangleElement element)
    {
        if (element.Fill is { } fill)
        {
            canvas.FillRectangle(element.X, element.Top, element.Width, element.Height, fill);
        }

        if (element.Outline is { } outline)
        {
            canvas.DrawRectangle(element.X, element.Top, element.Width, element.Height, outline, element.OutlineThickness);
        }
    }
}
