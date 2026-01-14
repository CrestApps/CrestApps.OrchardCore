using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDocumentIntentDetector"/> that uses heuristic-based
/// keyword pattern matching to classify user intent. This provides a lightweight, low-cost
/// alternative to using AI models for intent detection.
/// </summary>
public sealed class DefaultDocumentIntentDetector : IDocumentIntentDetector
{
    // Keyword patterns for different intents (case-insensitive matching)
    private static readonly string[] _summarizationKeywords =
    [
        "summarize", "summary", "summarise", "brief", "overview", "outline",
        "key points", "main points", "tldr", "tl;dr", "gist", "recap"
    ];

    private static readonly string[] _tabularAnalysisKeywords =
    [
        "calculate", "total", "sum", "average", "mean", "count", "aggregate",
        "analyze data", "analyse data", "statistics", "chart", "graph",
        "trend", "correlation", "breakdown", "distribution"
    ];

    private static readonly string[] _extractionKeywords =
    [
        "extract", "pull out", "get all", "list all", "find all",
        "parse", "structure", "json", "schema", "fields", "entities"
    ];

    private static readonly string[] _comparisonKeywords =
    [
        "compare", "difference", "differ", "similar", "same", "contrast",
        "versus", "vs", "between", "comparison"
    ];

    private static readonly string[] _transformationKeywords =
    [
        "convert", "transform", "format", "reformat", "change to",
        "make it", "turn into", "translate to", "bullet points", "table"
    ];

    private static readonly string[] _tabularFileExtensions =
    [
        ".csv", ".xlsx", ".xls", ".tsv"
    ];

    /// <inheritdoc />
    public Task<DocumentIntentResult> DetectIntentAsync(DocumentIntentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Prompt))
        {
            // Default to general chat with reference if no prompt provided
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.GeneralChatWithReference,
                0.5f,
                "No prompt provided, defaulting to general chat."));
        }

        var prompt = context.Prompt.ToLowerInvariant();
        var hasTabularFiles = HasTabularFiles(context.Documents);
        var hasMultipleDocuments = context.Documents.Count > 1;

        // Check for tabular analysis intent (high priority for CSV/Excel files)
        if (hasTabularFiles && ContainsAnyKeyword(prompt, _tabularAnalysisKeywords))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.AnalyzeTabularData,
                0.9f,
                "Tabular file detected with analysis-related keywords."));
        }

        // Check for comparison intent (requires multiple documents)
        if (hasMultipleDocuments && ContainsAnyKeyword(prompt, _comparisonKeywords))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.CompareDocuments,
                0.85f,
                "Multiple documents with comparison keywords detected."));
        }

        // Check for summarization intent
        if (ContainsAnyKeyword(prompt, _summarizationKeywords))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.SummarizeDocument,
                0.9f,
                "Summarization keywords detected."));
        }

        // Check for extraction intent
        if (ContainsAnyKeyword(prompt, _extractionKeywords))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.ExtractStructuredData,
                0.85f,
                "Data extraction keywords detected."));
        }

        // Check for transformation intent
        if (ContainsAnyKeyword(prompt, _transformationKeywords))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.TransformFormat,
                0.8f,
                "Transformation keywords detected."));
        }

        // Check for question-answering patterns (common RAG use case)
        if (IsQuestionPattern(prompt))
        {
            return Task.FromResult(DocumentIntentResult.FromIntent(
                DocumentIntent.DocumentQnA,
                0.75f,
                "Question pattern detected, using RAG approach."));
        }

        // Default to document Q&A (existing RAG behavior) for backward compatibility
        return Task.FromResult(DocumentIntentResult.FromIntent(
            DocumentIntent.DocumentQnA,
            0.5f,
            "No specific intent detected, defaulting to document Q&A."));
    }

    private static bool ContainsAnyKeyword(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasTabularFiles(IList<ChatInteractionDocument> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            return false;
        }

        foreach (var doc in documents)
        {
            if (string.IsNullOrEmpty(doc.FileName))
            {
                continue;
            }

            foreach (var ext in _tabularFileExtensions)
            {
                if (doc.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsQuestionPattern(string prompt)
    {
        // Check for common question patterns
        var questionStarters = new[]
        {
            "what", "where", "when", "who", "why", "how", "which",
            "can you", "could you", "tell me", "explain", "describe",
            "is there", "are there", "does", "do"
        };

        foreach (var starter in questionStarters)
        {
            if (prompt.StartsWith(starter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for question mark
        return prompt.Contains('?');
    }
}
