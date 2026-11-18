namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// The kinds of file a learning package may hold: web pages and their scripts, styles, data, images, fonts and media,
/// the manifests and their schemas, and documents. Anything else (programs, server pages, configuration) is refused at
/// import.
/// </summary>
public static class PackageFileTypes
{
    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".xhtml", ".js", ".mjs", ".css", ".json", ".xml", ".xsd", ".dtd", ".xsl", ".txt", ".csv", ".md",
        ".map", ".vtt", ".srt", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp", ".ico", ".avif", ".woff",
        ".woff2", ".ttf", ".otf", ".eot", ".mp3", ".mp4", ".m4a", ".m4v", ".webm", ".ogg", ".oga", ".ogv", ".wav", ".aac",
        ".flac", ".pdf", ".swf", ".wasm", ".glb", ".gltf", ".bin",
    };

    /// <summary>
    /// Tells whether a file of a package may be kept.
    /// </summary>
    /// <param name="path">The path in the package.</param>
    /// <returns><see langword="true"/> for a kind of file a package may hold.</returns>
    public static bool IsAllowed(string path)
        => !string.IsNullOrEmpty(path) && _extensions.Contains(Path.GetExtension(path));
}
