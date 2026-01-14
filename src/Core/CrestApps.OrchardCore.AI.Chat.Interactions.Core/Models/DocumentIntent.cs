namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Represents the detected intent when processing documents in a chat interaction.
/// </summary>
public enum DocumentIntent
{
    /// <summary>
    /// Question answering over documents (RAG - Retrieval-Augmented Generation).
    /// Uses vector search to find relevant chunks and inject them as context.
    /// </summary>
    DocumentQnA,

    /// <summary>
    /// Summarize the content of one or more documents.
    /// Bypasses vector search and streams content directly.
    /// </summary>
    SummarizeDocument,

    /// <summary>
    /// Analyze tabular data (CSV, Excel, etc.).
    /// Parses structured data and performs calculations or aggregations.
    /// </summary>
    AnalyzeTabularData,

    /// <summary>
    /// Extract structured data from documents.
    /// Focuses on schema extraction, reformatting, or conversion.
    /// </summary>
    ExtractStructuredData,

    /// <summary>
    /// Compare multiple documents.
    /// Analyzes differences, similarities, or relationships between documents.
    /// </summary>
    CompareDocuments,

    /// <summary>
    /// Transform or reformat document content.
    /// Converts files into other representations (tables, JSON, bullet points, etc.).
    /// </summary>
    TransformFormat,

    /// <summary>
    /// General chat with document reference.
    /// Fallback when no specific intent is detected.
    /// </summary>
    GeneralChatWithReference,
}
