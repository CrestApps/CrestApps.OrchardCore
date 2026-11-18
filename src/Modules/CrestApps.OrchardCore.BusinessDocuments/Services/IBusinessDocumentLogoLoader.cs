using CrestApps.OrchardCore.Pdf;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Reads the logo of a business document for its PDF. It never fetches an address from the network: only a file the
/// site holds can be read.
/// </summary>
internal interface IBusinessDocumentLogoLoader
{
    /// <summary>
    /// Reads a logo.
    /// </summary>
    /// <param name="logoUrl">The address of the logo.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The image, or <see langword="null"/> when the address is not a PNG or JPEG file the site holds.</returns>
    Task<PdfImage> LoadAsync(string logoUrl, CancellationToken cancellationToken = default);
}
