using PdfSharp.Fonts;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Points PDFsharp at the host's fonts, once per process.
/// </summary>
internal static class ReportPdfFonts
{
    private static readonly Lock _gate = new();

    private static volatile bool _configured;

    /// <summary>
    /// Configures font resolution if it has not been configured yet.
    /// </summary>
    /// <remarks>
    /// A caller that arrives while another is configuring waits for it: returning early would let it draw text
    /// before the resolver is in place, which PDFsharp fails with "No appropriate font found". A resolver another
    /// module already set, such as the one of CrestApps.Core's PDF documents, is kept: PDFsharp refuses to change it
    /// once a font was resolved.
    /// </remarks>
    public static void Ensure()
    {
        if (_configured)
        {
            return;
        }

        lock (_gate)
        {
            if (_configured)
            {
                return;
            }

            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;
                GlobalFontSettings.UseWindowsFontsUnderWsl2 = true;

                if (!OperatingSystem.IsWindows() && ReportPdfFontResolver.TryCreate(out var resolver))
                {
                    GlobalFontSettings.FontResolver = resolver;
                }
            }

            _configured = true;
        }
    }
}
