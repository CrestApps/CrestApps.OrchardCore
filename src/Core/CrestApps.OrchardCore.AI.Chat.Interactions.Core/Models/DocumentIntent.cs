namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Options for configuring document processing intents and their associated strategies.
/// </summary>
public sealed class ChatInteractionDocumentOptions
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HashSet<Type>> _intentStrategies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the mapping of intent names to their supported strategy types.
    /// </summary>
    public IReadOnlyDictionary<string, HashSet<Type>> IntentStrategies => _intentStrategies;

    /// <summary>
    /// Gets or sets the fallback intent to use when no specific intent is detected.
    /// Defaults to <see cref="DocumentIntents.DocumentQnA"/>.
    /// </summary>
    public string FallbackIntent { get; set; } = DocumentIntents.DocumentQnA;

    /// <summary>
    /// Adds a strategy type for a specific intent.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type.</typeparam>
    /// <param name="intent">The intent name.</param>
    public void AddStrategy<TStrategy>(string intent)
        where TStrategy : class, IDocumentProcessingStrategy
    {
        ArgumentException.ThrowIfNullOrEmpty(intent);

        lock (_lock)
        {
            if (!_intentStrategies.TryGetValue(intent, out var strategies))
            {
                strategies = [];
                _intentStrategies[intent] = strategies;
            }

            strategies.Add(typeof(TStrategy));
        }
    }

    /// <summary>
    /// Gets all registered intent names.
    /// </summary>
    public IEnumerable<string> GetIntents()
    {
        lock (_lock)
        {
            return _intentStrategies.Keys.ToList();
        }
    }
}

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
}
