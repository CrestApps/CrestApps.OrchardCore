namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Service for extracting text content from uploaded documents.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>
    /// Extracts text content from a document stream.
    /// </summary>
    /// <param name="stream">The document stream.</param>
    /// <param name="fileName">The original file name with extension.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <returns>The extracted text content.</returns>
    Task<string> ExtractAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Checks if the file type is supported for text extraction.
    /// </summary>
    /// <param name="fileName">The file name with extension.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    bool IsSupported(string fileName, string contentType);
}

/// <summary>
/// Default implementation of document text extraction.
/// Supports plain text files. For PDF, Office documents, etc., 
/// additional NuGet packages would need to be added.
/// </summary>
public sealed class DefaultDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".csv",
        ".md",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log",
        ".yaml",
        ".yml",
    };

    private static readonly HashSet<string> _supportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
        "text/markdown",
        "text/html",
        "text/xml",
        "application/json",
        "application/xml",
    };

    public bool IsSupported(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);

        return _supportedExtensions.Contains(extension) || _supportedContentTypes.Contains(contentType);
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);

        // For text-based files, read directly
        if (IsTextFile(extension, contentType))
        {
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync();
        }

        // For unsupported formats, return empty
        // In production, you would add libraries for PDF, Office docs, etc.
        return string.Empty;
    }

    private static bool IsTextFile(string extension, string contentType)
    {
        return _supportedExtensions.Contains(extension) ||
               contentType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true ||
               _supportedContentTypes.Contains(contentType);
    }
}
