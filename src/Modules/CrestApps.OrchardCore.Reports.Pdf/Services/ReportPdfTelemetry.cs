using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// The telemetry of the PDF export, on the report meter <c>CrestApps.OrchardCore.Reports</c>: the exports written
/// with the built-in Helvetica font because no TrueType font could be used, which cannot show text outside Latin-1.
/// </summary>
internal static partial class ReportPdfTelemetry
{
    private static int _fallbackLogged;

    /// <summary>
    /// Gets the report meter. A second meter with the same name as the one of Reports.Core is listened to with it.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Reports");

    /// <summary>
    /// Gets the counter of the PDF exports written with the built-in Helvetica font.
    /// </summary>
    public static Counter<long> FontFallbacks { get; } = Meter.CreateCounter<long>(
        "crestapps.report.pdf.font_fallbacks",
        "{export}",
        "The PDF report exports written with the built-in Helvetica font because no TrueType font could be used.");

    /// <summary>
    /// Records a PDF export written with the built-in font, and logs a warning the first time.
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
        EventId = CrestAppsEventIds.Reports + 400,
        Level = LogLevel.Warning,
        Message = "No TrueType font could be used for PDF report exports, so they use the built-in Helvetica font, which cannot show text outside Latin-1. Install a font such as DejaVu Sans or Noto Sans on the server. This is logged once.")]
    private static partial void FontFallbackUsed(ILogger logger);
}
