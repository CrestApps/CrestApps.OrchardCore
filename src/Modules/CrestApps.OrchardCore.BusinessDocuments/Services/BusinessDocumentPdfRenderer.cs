using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.Pdf;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// The default <see cref="IBusinessDocumentPdfRenderer"/>, written with PdfPig on the shared PDF layout primitives. The
/// logo is read only from the site's media library, never from the network.
/// </summary>
internal sealed class BusinessDocumentPdfRenderer : IBusinessDocumentPdfRenderer
{
    private readonly IBusinessDocumentLogoLoader _logoLoader;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public BusinessDocumentPdfRenderer(
        IBusinessDocumentLogoLoader logoLoader,
        IStringLocalizer<BusinessDocumentPdfRenderer> stringLocalizer,
        ILogger<BusinessDocumentPdfRenderer> logger)
    {
        _logoLoader = logoLoader;
        S = stringLocalizer;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the font files the documents are written in; the installed fonts unless a test sets others.
    /// </summary>
    internal PdfFonts Fonts { get; set; } = PdfFonts.Installed;

    public async Task<byte[]> RenderAsync(BusinessDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var logo = string.IsNullOrWhiteSpace(document.LogoUrl)
            ? null
            : await _logoLoader.LoadAsync(document.LogoUrl, cancellationToken);

        // Without a TrueType font the PDF falls back to Helvetica, which shows only Latin-1 text.
        if (Fonts.Regular is null)
        {
            BusinessDocumentPdfTelemetry.RecordFontFallback(_logger);
        }

        using var culture = BusinessDocumentCulture.Use(document.Culture);

        var labels = new BusinessDocumentPdfLabels(
            S["Bill to"].Value,
            S["Ship to"].Value,
            S["Tax ID: {0}"].Value,
            S["Page {0} of {1}"].Value);

        try
        {
            return new BusinessDocumentPdfWriter(document, labels, Fonts, logo).Build();
        }
        catch (Exception exception) when (logo is not null && exception is not OperationCanceledException)
        {
            // An image PdfPig cannot embed, such as an unusual PNG, never stops the document: it is written without it.
            BusinessDocumentPdfTelemetry.LogoNotDrawn(_logger, exception);

            return new BusinessDocumentPdfWriter(document, labels, Fonts, logo: null).Build();
        }
    }
}
