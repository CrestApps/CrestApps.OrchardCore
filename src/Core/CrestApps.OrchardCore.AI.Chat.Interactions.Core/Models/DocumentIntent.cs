namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Well-known document processing intent names.
/// </summary>
public static class DocumentIntents
{
    /// <summary>
    /// Question answering over documents (RAG - Retrieval-Augmented Generation).
    /// Uses vector search to find relevant chunks and inject them as context.
    /// </summary>
    public const string DocumentQnA = "DocumentQnA";

    /// <summary>
    /// Summarize the content of one or more documents.
    /// Bypasses vector search and streams content directly.
    /// </summary>
    public const string SummarizeDocument = "SummarizeDocument";

    /// <summary>
    /// Analyze tabular data (CSV, Excel, etc.).
    /// Parses structured data and performs calculations or aggregations.
    /// </summary>
    public const string AnalyzeTabularData = "AnalyzeTabularData";

    /// <summary>
    /// Extract structured data from documents.
    /// Focuses on schema extraction, reformatting, or conversion.
    /// </summary>
    public const string ExtractStructuredData = "ExtractStructuredData";

    /// <summary>
    /// Compare multiple documents.
    /// Analyzes differences, similarities, or relationships between documents.
    /// </summary>
    public const string CompareDocuments = "CompareDocuments";

    /// <summary>
    /// Transform or reformat document content.
    /// Converts files into other representations (tables, JSON, bullet points, etc.).
    /// </summary>
    public const string TransformFormat = "TransformFormat";

    /// <summary>
    /// General chat with document reference.
    /// Fallback when no specific intent is detected.
    /// </summary>
    public const string GeneralChatWithReference = "GeneralChatWithReference";

    /// <summary>
    /// Generate an image based on a text description.
    /// Uses AI image generation models (e.g., DALL-E) to create visual content.
    /// </summary>
    public const string GenerateImage = "GenerateImage";
}
