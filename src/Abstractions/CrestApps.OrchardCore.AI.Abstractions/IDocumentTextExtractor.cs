namespace CrestApps.OrchardCore.AI;

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
    /// <param name="fileExtension">The extension of the file.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <returns>The extracted text content.</returns>
    Task<string> ExtractAsync(Stream stream, string fileName, string fileExtension, string contentType);
}
