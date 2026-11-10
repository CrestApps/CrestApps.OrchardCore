using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Serializes a report document to a PDF laid out like the report on screen: the title, then every section in order,
/// with metrics as cards, tables with their headers repeated on every page and their cell styles, bars as progress
/// bars, and charts drawn as charts. Pages are numbered, and a report with a wide table is laid out in landscape.
/// </summary>
public sealed class PdfReportExportFormat : IReportExportFormat
{
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfReportExportFormat"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer of the text the format writes, such as the page numbers.</param>
    /// <param name="logger">The logger, or <see langword="null"/> to log nothing.</param>
    public PdfReportExportFormat(
        IStringLocalizer<PdfReportExportFormat> stringLocalizer,
        ILogger<PdfReportExportFormat> logger = null)
    {
        S = stringLocalizer;
        _logger = logger ?? NullLogger<PdfReportExportFormat>.Instance;
    }

    /// <inheritdoc/>
    public static string Name => ReportsConstants.PdfExportFormat;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PdfReportExportFormat>("PDF");

    /// <inheritdoc/>
    public static string Extension => ".pdf";

    /// <inheritdoc/>
    public static string ContentType => "application/pdf";

    /// <inheritdoc/>
    public byte[] Serialize(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fonts = ReportPdfFonts.Installed;

        // Without a TrueType font the PDF falls back to Helvetica, which shows only Latin-1 text.
        if (fonts.Regular is null)
        {
            ReportPdfTelemetry.RecordFontFallback(_logger);
        }

        return new ReportPdfDocumentBuilder(document, S["Page {0} of {1}"].Value, fonts).Build();
    }
}
