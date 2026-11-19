using CrestApps.OrchardCore.Pdf;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Reads the logo of a business document from the site's media library. The address must point into the media
/// library, as a relative address, an absolute address whose path is the media library's, or a media path, and name a
/// PNG or JPEG file. Nothing is fetched from the network, so a document cannot make the server call an address
/// someone typed.
/// </summary>
internal sealed class MediaLibraryLogoLoader : IBusinessDocumentLogoLoader
{
    /// <summary>
    /// The largest logo file read, in bytes.
    /// </summary>
    internal const long MaximumLength = 2 * 1024 * 1024;

    private static readonly string[] _extensions = [".png", ".jpg", ".jpeg"];

    private readonly IMediaFileStore _mediaFileStore;

    public MediaLibraryLogoLoader(IMediaFileStore mediaFileStore)
    {
        _mediaFileStore = mediaFileStore;
    }

    public async Task<PdfImage> LoadAsync(string logoUrl, CancellationToken cancellationToken = default)
    {
        var path = GetMediaPath(logoUrl, _mediaFileStore.MapPathToPublicUrl(string.Empty));

        if (path is null || !_extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var file = await _mediaFileStore.GetFileInfoAsync(path);

        if (file is null || file.IsDirectory || file.Length > MaximumLength)
        {
            return null;
        }

        await using var stream = await _mediaFileStore.GetFileStreamAsync(path);
        using var content = new MemoryStream();

        await stream.CopyToAsync(content, cancellationToken);

        if (content.Length > MaximumLength)
        {
            return null;
        }

        return PdfImage.TryRead(content.ToArray(), out var image) ? image : null;
    }

    /// <summary>
    /// Gets the path in the media library an address points to.
    /// </summary>
    /// <param name="url">The address, such as <c>/media/logo.png</c>, <c>https://example.com/media/logo.png</c> or
    /// <c>logo.png</c>.</param>
    /// <param name="mediaBaseUrl">The public address of the media library's root, such as <c>/media/</c>.</param>
    /// <returns>The path, such as <c>logo.png</c>, or <see langword="null"/> when the address does not point into the
    /// media library.</returns>
    internal static string GetMediaPath(string url, string mediaBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var address = url.Trim();
        var end = address.IndexOfAny(['?', '#']);

        if (end >= 0)
        {
            address = address.Substring(0, end);
        }

        var baseUrl = string.IsNullOrWhiteSpace(mediaBaseUrl) ? "/media/" : mediaBaseUrl.Trim();

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        var basePath = Uri.TryCreate(baseUrl, UriKind.Absolute, out var absoluteBase) ? absoluteBase.AbsolutePath : baseUrl;
        string path;

        if (Uri.TryCreate(address, UriKind.Absolute, out var absolute) && !address.StartsWith('/'))
        {
            if (absolute.Scheme != Uri.UriSchemeHttps && absolute.Scheme != Uri.UriSchemeHttp)
            {
                return null;
            }

            path = StripPrefix(absolute.AbsolutePath, basePath);
        }
        else if (address.StartsWith("//", StringComparison.Ordinal))
        {
            path = Uri.TryCreate("https:" + address, UriKind.Absolute, out var protocolRelative)
                ? StripPrefix(protocolRelative.AbsolutePath, basePath)
                : null;
        }
        else if (address.StartsWith('/'))
        {
            path = StripPrefix(address, basePath);
        }
        else
        {
            // A path in the media library, as a media field stores it.
            path = address;
        }

        if (path is null)
        {
            return null;
        }

        path = Uri.UnescapeDataString(path).Replace('\\', '/').Trim('/');

        if (path.Length == 0 || path.Split('/').Any(segment => segment is "." or ".." || segment.Length == 0) || path.Contains(':'))
        {
            return null;
        }

        return path;
    }

    private static string StripPrefix(string path, string prefix)
        => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path.Substring(prefix.Length) : null;
}
