using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Media;

/// <summary>
/// What the media tools share: the permission pair every Media Library action checks, the check of a media path, the
/// check of a file extension, and the fields a tool returns for a file.
/// </summary>
internal static class MediaToolSupport
{
    /// <summary>
    /// The MIME type of a file whose extension is not known.
    /// </summary>
    public const string DefaultMimeType = "application/octet-stream";

    /// <summary>
    /// The extensions of the files that hold plain text. The tools read and write the content of these files only, and
    /// only where the site also allows the extension.
    /// </summary>
    public static readonly IReadOnlySet<string> TextFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".css",
        ".csv",
        ".htm",
        ".html",
        ".js",
        ".json",
        ".md",
        ".srt",
        ".svg",
        ".tsv",
        ".txt",
        ".vtt",
        ".xml",
        ".yaml",
        ".yml",
    };

    private static readonly char[] _pathSeparators = ['/', '\\'];

    /// <summary>
    /// Whether the signed-in caller may use the Media Library at all. Every Media Library action checks it first.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission to manage media.</returns>
    public static Task<bool> CanManageMediaAsync(IServiceProvider services)
        => AgentToolAuthorization.IsAuthorizedAsync(services, MediaPermissions.ManageMedia);

    /// <summary>
    /// Whether the signed-in caller may manage a folder or a file of the media library. The path is the resource, as in
    /// the Media Library: Orchard Core maps it to the permission for normal folders, the attached media folder, the
    /// caller's own folder or the folders of other users, and to the view permission of the folder with Secure Media.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="path">The checked media path. An empty path is the root folder.</param>
    /// <returns><see langword="true"/> when a signed-in caller may manage the path.</returns>
    public static Task<bool> CanManagePathAsync(IServiceProvider services, string path)
        => AgentToolAuthorization.IsAuthorizedAsync(services, MediaPermissions.ManageMediaFolder, (object)(path ?? string.Empty));

    /// <summary>
    /// Whether the signed-in caller may store files with a restricted extension.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission to upload restricted media.</returns>
    public static Task<bool> CanUploadRestrictedMediaAsync(IServiceProvider services)
        => AgentToolAuthorization.IsAuthorizedAsync(services, MediaPermissions.UploadRestrictedMedia);

    /// <summary>
    /// Checks the path of a media folder the model gave and puts it in the form of the media store: forward slashes, no
    /// slash at either end. A path that could mean another place to the permission check than to the store is refused:
    /// a drive or scheme, a <c>.</c> or <c>..</c> segment, an empty segment, and URL-encoded characters.
    /// </summary>
    /// <param name="mediaFileStore">The media store.</param>
    /// <param name="path">The path the model gave. Nothing means the root folder.</param>
    /// <param name="normalized">The checked path. It is empty for the root folder.</param>
    /// <returns><see langword="false"/> when the path cannot be used.</returns>
    public static bool TryNormalizeFolderPath(IMediaFileStore mediaFileStore, string path, out string normalized)
        => TryNormalizePath(mediaFileStore, path, isFile: false, out normalized);

    /// <summary>
    /// Checks the path of a media file the model gave and puts it in the form of the media store. The folder of the
    /// file is checked as <see cref="TryNormalizeFolderPath"/> checks a folder. The name of the file may hold a percent
    /// sign, as the name of an uploaded file can (<c>Annual%20Report.pdf</c>), as long as it is still one file name
    /// when it is decoded.
    /// </summary>
    /// <param name="mediaFileStore">The media store.</param>
    /// <param name="path">The path the model gave.</param>
    /// <param name="normalized">The checked path.</param>
    /// <returns><see langword="false"/> when the path is empty or cannot be used.</returns>
    public static bool TryNormalizeFilePath(IMediaFileStore mediaFileStore, string path, out string normalized)
        => TryNormalizePath(mediaFileStore, path, isFile: true, out normalized) && normalized.Length > 0;

    /// <summary>
    /// Says why a path was refused, for the result of a tool.
    /// </summary>
    /// <param name="path">The path the model gave.</param>
    /// <returns>The sentence.</returns>
    public static string DescribeInvalidPath(string path)
        => $"'{path}' is not a media path. Give the path from the root of the media library, for example brand/logo.png, without a drive or a '..' segment, and not URL-encoded.";

    /// <summary>
    /// Gets the folder part of a media path.
    /// </summary>
    /// <param name="path">A checked media path of a file.</param>
    /// <returns>The folder, which is empty for a file in the root folder.</returns>
    public static string GetFolder(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var index = path.LastIndexOf('/');

        return index < 0
            ? string.Empty
            : path[..index];
    }

    /// <summary>
    /// Whether the site accepts a file extension from the caller: an allowed extension, or a restricted one when the
    /// caller may upload restricted media. This is the rule of the Media Library upload.
    /// </summary>
    /// <param name="mediaOptions">The media options.</param>
    /// <param name="extension">The extension, with its leading dot.</param>
    /// <param name="canUploadRestrictedMedia">Whether the caller may upload restricted media.</param>
    /// <returns><see langword="true"/> when the extension is accepted.</returns>
    public static bool IsExtensionAllowed(MediaOptions mediaOptions, string extension, bool canUploadRestrictedMedia)
    {
        ArgumentNullException.ThrowIfNull(mediaOptions);

        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        return mediaOptions.AllowedFileExtensions?.Contains(extension) == true ||
            (canUploadRestrictedMedia && mediaOptions.RestrictedFileExtensions?.Contains(extension) == true);
    }

    /// <summary>
    /// Says why an extension was refused, for the result of a tool.
    /// </summary>
    /// <param name="mediaOptions">The media options.</param>
    /// <param name="extension">The refused extension.</param>
    /// <returns>The sentence.</returns>
    public static string DescribeRefusedExtension(MediaOptions mediaOptions, string extension)
    {
        ArgumentNullException.ThrowIfNull(mediaOptions);

        if (string.IsNullOrEmpty(extension))
        {
            return "the file name needs an extension, for example .png.";
        }

        return mediaOptions.RestrictedFileExtensions?.Contains(extension) == true
            ? $"the extension '{extension}' is restricted on this site and you do not hold the permission to upload restricted media."
            : $"the extension '{extension}' is not allowed on this site. Use getMediaOptions to see the allowed extensions.";
    }

    /// <summary>
    /// Checks the path a file is moved or copied to, with the rules of the Media Library: the extension is accepted
    /// from the caller and nothing is stored at the path yet. The folder must exist, because a file is moved or copied
    /// into a folder of the library.
    /// </summary>
    /// <param name="mediaFileStore">The media store.</param>
    /// <param name="mediaOptions">The media options.</param>
    /// <param name="newPath">The checked media path of the new file.</param>
    /// <param name="canUploadRestrictedMedia">Whether the caller may upload restricted media.</param>
    /// <returns>What is wrong with the path as a sentence, or <see langword="null"/> when the path can be used.</returns>
    public static async Task<string> CheckNewFilePathAsync(IMediaFileStore mediaFileStore, MediaOptions mediaOptions, string newPath, bool canUploadRestrictedMedia)
    {
        ArgumentNullException.ThrowIfNull(mediaFileStore);
        ArgumentNullException.ThrowIfNull(mediaOptions);
        ArgumentException.ThrowIfNullOrEmpty(newPath);

        var extension = Path.GetExtension(newPath);

        if (!IsExtensionAllowed(mediaOptions, extension, canUploadRestrictedMedia))
        {
            return $"'{newPath}' cannot be used: {DescribeRefusedExtension(mediaOptions, extension)}";
        }

        var folder = GetFolder(newPath);

        if (folder.Length > 0 && await mediaFileStore.GetDirectoryInfoAsync(folder) is null)
        {
            return $"There is no media folder '{folder}'. Create the folder in the Media Library first, or use a folder that listMediaFiles returns.";
        }

        if (await mediaFileStore.GetFileInfoAsync(newPath) is not null)
        {
            return $"A media file already exists at '{newPath}'.";
        }

        return await mediaFileStore.GetDirectoryInfoAsync(newPath) is not null
            ? $"'{newPath}' is a folder."
            : null;
    }

    /// <summary>
    /// Gets the MIME type of a file from its name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="fileName">The name or path of the file.</param>
    /// <returns>The MIME type.</returns>
    public static string GetMimeType(IServiceProvider services, string fileName)
    {
        ArgumentNullException.ThrowIfNull(services);

        var contentTypeProvider = services.GetService<IContentTypeProvider>();

        return contentTypeProvider is not null && contentTypeProvider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : DefaultMimeType;
    }

    /// <summary>
    /// Builds the fields a tool returns for a media file.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="mediaFileStore">The media store.</param>
    /// <param name="file">The file.</param>
    /// <returns>The fields of the file, with its date in the user's or the site's time zone.</returns>
    public static async Task<MediaFileSummary> DescribeFileAsync(IServiceProvider services, IMediaFileStore mediaFileStore, IFileStoreEntry file)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mediaFileStore);
        ArgumentNullException.ThrowIfNull(file);

        var path = mediaFileStore.NormalizePath(file.Path);

        return new MediaFileSummary
        {
            Path = path,
            Name = file.Name,
            Folder = GetFolder(path),
            Size = file.Length,
            MimeType = GetMimeType(services, file.Name),
            LastModified = await AgentToolResults.ToLocalAsync(services.GetRequiredService<ILocalClock>(), file.LastModifiedUtc),
            Url = mediaFileStore.MapPathToPublicUrl(path),
        };
    }

    /// <summary>
    /// Copies a new or moved file into the local cache of a remote media store, as the Media Library does, so the
    /// first request for it does not wait for the remote store. Nothing happens on a site without a remote store.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="mediaFileStore">The media store.</param>
    /// <param name="file">The stored file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the file is cached.</returns>
    public static async Task PreCacheAsync(IServiceProvider services, IMediaFileStore mediaFileStore, IFileStoreEntry file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(mediaFileStore);

        if (file is null || services.GetService<IMediaFileStoreCache>() is not { } mediaFileStoreCache)
        {
            return;
        }

        try
        {
            await using var stream = await mediaFileStore.GetFileStreamAsync(file);

            await mediaFileStoreCache.SetCacheAsync(stream, file, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The file is stored, so a cache that cannot be filled does not fail the tool, as in the Media Library. The
            // cache is filled on the first request for the file.
            services.GetService<ILoggerFactory>()?
                .CreateLogger(typeof(MediaToolSupport))
                .LogWarning(exception, "The media file could not be copied to the local cache of the remote media store.");
        }
    }

    private static bool TryNormalizePath(IMediaFileStore mediaFileStore, string path, bool isFile, out string normalized)
    {
        ArgumentNullException.ThrowIfNull(mediaFileStore);

        normalized = mediaFileStore.NormalizePath(path?.Trim()) ?? string.Empty;

        if (normalized.Length == 0)
        {
            return true;
        }

        if (!IsPlainText(normalized) || !normalized.Split(_pathSeparators).All(IsSegment))
        {
            return false;
        }

        // The permission check decodes the path and the store does not. A folder must read the same to both, so that a
        // permission is never checked on one folder and used on another.
        var folder = isFile
            ? GetFolder(normalized)
            : normalized;

        if (!string.Equals(Uri.UnescapeDataString(folder), folder, StringComparison.Ordinal))
        {
            return false;
        }

        if (!isFile)
        {
            return true;
        }

        // The decoded file name must still be one name in that folder.
        var name = Uri.UnescapeDataString(normalized[(normalized.LastIndexOf('/') + 1)..]);

        return IsPlainText(name) && name.IndexOfAny(_pathSeparators) < 0 && IsSegment(name);
    }

    private static bool IsPlainText(string path)
        => !path.Contains(':') && !path.Any(char.IsControl);

    private static bool IsSegment(string segment)
        => !string.IsNullOrWhiteSpace(segment) && !segment.EndsWith('.') && !segment.EndsWith(' ');
}
