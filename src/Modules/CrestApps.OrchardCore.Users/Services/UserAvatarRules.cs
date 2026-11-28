using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// The rules of a user's avatar: which media files can be an avatar. The avatar editor and the AI tool apply the same
/// rules; each words its own messages.
/// </summary>
internal static class UserAvatarRules
{
    /// <summary>
    /// Returns the file extensions an avatar may have: the media library's allowed extensions whose content type is an
    /// image.
    /// </summary>
    /// <param name="mediaOptions">The media options.</param>
    /// <param name="contentTypeProvider">The content type provider.</param>
    /// <returns>The extensions, with their leading dot.</returns>
    public static string[] GetAllowedExtensions(MediaOptions mediaOptions, IContentTypeProvider contentTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(mediaOptions);
        ArgumentNullException.ThrowIfNull(contentTypeProvider);

        var extensions = new List<string>();

        foreach (var extension in mediaOptions.AllowedFileExtensions)
        {
            if (contentTypeProvider.TryGetContentType(extension, out var contentType) && contentType.StartsWith("image/"))
            {
                extensions.Add(extension);
            }
        }

        return extensions.ToArray();
    }

    /// <summary>
    /// Returns whether a media path has an extension an avatar may have. With no allowed extensions, every path is allowed.
    /// </summary>
    /// <param name="path">The media path.</param>
    /// <param name="allowedExtensions">The allowed extensions.</param>
    /// <returns><see langword="true"/> when the path's extension is allowed.</returns>
    public static bool IsAllowedExtension(string path, string[] allowedExtensions)
        => allowedExtensions is not { Length: > 0 } || allowedExtensions.Contains(Path.GetExtension(path));
}
