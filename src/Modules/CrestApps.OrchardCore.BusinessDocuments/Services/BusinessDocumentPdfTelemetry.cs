using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// The telemetry of the business document PDF renderer, on the meter <c>CrestApps.OrchardCore.BusinessDocuments</c>:
/// the documents written with the built-in Helvetica font because no TrueType font could be used, which cannot show
/// text outside Latin-1.
/// </summary>
internal static partial class BusinessDocumentPdfTelemetry
{
    private static int _fallbackLogged;

    /// <summary>
    /// Gets the business documents meter.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("BusinessDocuments");

    /// <summary>
    /// Gets the counter of the PDF documents written with the built-in Helvetica font.
    /// </summary>
    public static Counter<long> FontFallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.business_document.pdf.font_fallbacks",
        "{document}",
        "The business document PDFs written with the built-in Helvetica font because no TrueType font could be used.");

    /// <summary>
    /// Records a PDF written with the built-in font, and logs a warning the first time.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public static void RecordFontFallback(ILogger logger)
    {
        if (FontFallbacks.Enabled)
        {
            var tags = new TagList();

            CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
            FontFallbacks.Add(1, tags);
        }

        if (Interlocked.Exchange(ref _fallbackLogged, 1) == 0)
        {
            FontFallbackUsed(logger);
        }
    }

    [LoggerMessage(
        EventId = CrestAppsEventIds.Commerce + 900,
        Level = LogLevel.Warning,
        Message = "No TrueType font could be used for business document PDFs, so they use the built-in Helvetica font, which cannot show text outside Latin-1. Install a font such as DejaVu Sans or Noto Sans on the server. This is logged once.")]
    private static partial void FontFallbackUsed(ILogger logger);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Commerce + 901,
        Level = LogLevel.Warning,
        Message = "The logo of a business document could not be drawn in its PDF, which is written without it.")]
    public static partial void LogoNotDrawn(ILogger logger, Exception exception);
}
