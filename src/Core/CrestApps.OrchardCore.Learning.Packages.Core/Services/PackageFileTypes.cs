namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// The kinds of file a learning package may hold, each with the content type it is served with: web pages and their
/// scripts, styles, data, images, cursors, fonts, captions and media, the manifests and their schemas, and documents
/// (PDF and Office files without macros). Anything else (programs, scripts a server or a shell runs, server pages,
/// configuration) is refused at import.
/// </summary>
public static class PackageFileTypes
{
    private const string OctetStream = "application/octet-stream";

    private static readonly Dictionary<string, string> _contentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Pages, scripts and styles.
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".xhtml"] = "application/xhtml+xml",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".map"] = "application/json",
        [".wasm"] = "application/wasm",

        // Data, manifests and their schemas.
        [".json"] = "application/json",
        [".xml"] = "text/xml",
        [".xsd"] = "text/xml",
        [".dtd"] = "application/xml-dtd",
        [".xsl"] = "text/xml",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".md"] = "text/markdown",

        // Captions.
        [".vtt"] = "text/vtt",
        [".srt"] = "text/plain",

        // Images, icons and cursors.
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".avif"] = "image/avif",
        [".ico"] = "image/x-icon",
        [".cur"] = "image/x-icon",

        // Fonts.
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".eot"] = "application/vnd.ms-fontobject",

        // Audio and video.
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".flac"] = "audio/flac",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogv"] = "video/ogg",
        [".swf"] = "application/x-shockwave-flash",

        // 3D models.
        [".glb"] = "model/gltf-binary",
        [".gltf"] = "model/gltf+json",
        [".bin"] = OctetStream,

        // Documents: PDF and Office files without macros (Rise attachments, handouts).
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    /// <summary>
    /// Gets the extensions a package may hold, with their dot.
    /// </summary>
    public static IReadOnlyCollection<string> Extensions => _contentTypes.Keys;

    /// <summary>
    /// Tells whether a file of a package may be kept.
    /// </summary>
    /// <param name="path">The path in the package.</param>
    /// <returns><see langword="true"/> for a kind of file a package may hold.</returns>
    public static bool IsAllowed(string path)
        => !string.IsNullOrEmpty(path) && _contentTypes.ContainsKey(Path.GetExtension(path));

    /// <summary>
    /// Gets the content type a file of a package is served with.
    /// </summary>
    /// <param name="path">The path in the package.</param>
    /// <returns>The content type of its extension, or <c>application/octet-stream</c>.</returns>
    public static string ContentTypeOf(string path)
        => !string.IsNullOrEmpty(path) && _contentTypes.TryGetValue(Path.GetExtension(path), out var contentType)
            ? contentType
            : OctetStream;
}
