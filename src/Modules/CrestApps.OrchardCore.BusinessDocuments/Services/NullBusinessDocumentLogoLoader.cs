using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// The logo loader of a site without the media library: PDFs are written without a logo.
/// </summary>
internal sealed class NullBusinessDocumentLogoLoader : IBusinessDocumentLogoLoader
{
    public Task<PdfImage> LoadAsync(string logoUrl, CancellationToken cancellationToken = default)
        => Task.FromResult<PdfImage>(null);
}
