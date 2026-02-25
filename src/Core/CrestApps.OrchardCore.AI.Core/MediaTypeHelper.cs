namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides media type inference from file extensions for <see cref="Microsoft.Extensions.DataIngestion.IngestionDocumentReader"/> dispatch.
/// </summary>
public static class MediaTypeHelper
{
    /// <summary>
    /// Infers the media type for a file extension, falling back to the provided content type
    /// or <c>text/plain</c> if no mapping is found.
    /// </summary>
    /// <param name="extension">The file extension (e.g., <c>.pdf</c>).</param>
    /// <param name="fallbackContentType">An optional fallback content type from the HTTP request.</param>
    /// <returns>The inferred media type string.</returns>
    public static string InferMediaType(string extension, string fallbackContentType = null)
    {
        var mediaType = extension?.ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".html" or ".htm" => "text/html",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".csv" => "text/csv",
            ".yaml" or ".yml" => "text/yaml",
            _ => null,
        };

        return mediaType ?? fallbackContentType ?? "text/plain";
    }
}
