using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keyword-based implementation of <see cref="IPromptIntentDetector"/> that uses heuristic-based
/// pattern matching to classify user intent. This provides a lightweight, low-cost
/// fallback alternative when AI-based intent detection is unavailable.
/// </summary>
public sealed class KeywordPromptIntentDetector : IPromptIntentDetector
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
        "analyze data", "analyse data", "statistics",
        "trend", "correlation", "breakdown", "distribution"
    ];

    private static readonly string[] _rowLevelTabularAnalysisKeywords =
    [
        "for every row", "for each row", "per row", "row by row", "row-by-row",
        "each record", "per record"
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

    private static readonly string[] _imageGenerationKeywords =
    [
        "generate image", "create image", "generate a picture",
        "create a picture", "make an image", "generate an illustration",
        "create an illustration", "generate artwork", "create artwork",
        "generate a visual", "create a visual", "image of", "picture of",
        "illustration of", "render an image",
        "design an image", "generate a photo", "create a photo",
        "draw a", "draw an", "draw "
    ];

    private static readonly string[] _imageGenerationWithHistoryKeywords =
    [
        "generate an image from", "create an image from", "make an image from",
        "generate an image based on", "create an image based on", "make an image based on",
        "generate a picture from", "create a picture from", "make a picture from"
    ];

    private static readonly string[] _chartGenerationKeywords =
    [
        "create a chart", "create chart", "draw a chart", "draw chart",
        "bar chart", "line chart", "pie chart", "scatter plot", "doughnut chart",
        "histogram", "area chart", "radar chart",
        "plot", "create a plot", "draw a plot",
        "create a graph", "draw a graph", "make a graph",
        "generate a chart", "generate chart", "render a chart",
        "make a chart", "make chart", "show a chart", "show chart",
        "visualize data", "visualise data", "data visualization",
        "chart of", "graph of", "plot of"
    ];

    private static readonly string[] _historyReferenceCuePhrases =
    [
        "use that",
        "use this",
        "use it",
        "use them",
        "that data",
        "this data",
        "the data",
        "that table",
        "this table",
        "that chart",
        "this chart",
        "based on that",
        "based on this",
        "from that",
        "from this",
        "from the",
        "previous",
        "earlier",
        "above",
        "as discussed",
        "as shown",
        "representing that",
        "representing this",
    ];

    private static readonly string[] _tabularFileExtensions =
    [
        ".csv", ".xlsx", ".xls", ".tsv"
    ];

    /// <inheritdoc />
    public Task<DocumentIntent> DetectAsync(DocumentIntentDetectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(context.Prompt))
        {
            // Default to general chat with reference if no prompt provided
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.GeneralChatWithReference,
                0.5f,
                "No prompt provided, defaulting to general chat."));
        }

        var prompt = context.Prompt.ToLowerInvariant();
        var hasTabularFiles = HasTabularFiles(context.Documents);
        var hasMultipleDocuments = context.Documents.Count > 1;
        var referencesHistory = ContainsAnyKeyword(prompt, _historyReferenceCuePhrases);

        // Check for chart generation intent (high priority - chart keywords are specific)
        // Always use GenerateChart since the AI model already has conversation history
        if (ContainsAnyKeyword(prompt, _chartGenerationKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.GenerateChart,
                0.9f,
                "Chart generation keywords detected."));
        }

        // Check for image generation intent (doesn't require documents)
        // Special-case phrases like "generate an image from that table" which can otherwise match transform keywords.
        if (ContainsAnyKeyword(prompt, _imageGenerationWithHistoryKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.GenerateImageWithHistory,
                0.9f,
                "Image generation with history keywords detected."));
        }

        if (ContainsAnyKeyword(prompt, _imageGenerationKeywords))
        {
            var intentName = referencesHistory
                ? DocumentIntents.GenerateImageWithHistory
                : DocumentIntents.GenerateImage;

            return Task.FromResult(DocumentIntent.FromName(
                intentName,
                0.9f,
                "Image generation keywords detected."));
        }

        // Check for row-level tabular analysis intent (highest priority for CSV/Excel files)
        if (hasTabularFiles && ContainsAnyKeyword(prompt, _rowLevelTabularAnalysisKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.AnalyzeTabularDataByRow,
                0.9f,
                "Tabular file detected with row-level processing keywords."));
        }

        // Check for tabular analysis intent (high priority for CSV/Excel files)
        if (hasTabularFiles && ContainsAnyKeyword(prompt, _tabularAnalysisKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.AnalyzeTabularData,
                0.9f,
                "Tabular file detected with analysis-related keywords."));
        }

        // Check for comparison intent (requires multiple documents)
        if (hasMultipleDocuments && ContainsAnyKeyword(prompt, _comparisonKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.CompareDocuments,
                0.85f,
                "Multiple documents with comparison keywords detected."));
        }

        // Check for summarization intent
        if (ContainsAnyKeyword(prompt, _summarizationKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.SummarizeDocument,
                0.9f,
                "Summarization keywords detected."));
        }

        // Check for extraction intent
        if (ContainsAnyKeyword(prompt, _extractionKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.ExtractStructuredData,
                0.9f,
                "Extraction keywords detected."));
        }

        // Check for transformation intent
        if (ContainsAnyKeyword(prompt, _transformationKeywords))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.TransformFormat,
                0.85f,
                "Transformation keywords detected."));
        }

        // Default to DocumentQnA for question patterns or general queries
        if (IsQuestionPattern(prompt))
        {
            return Task.FromResult(DocumentIntent.FromName(
                DocumentIntents.DocumentQnA,
                0.8f,
                "Question pattern detected."));
        }

        return Task.FromResult(DocumentIntent.FromName(
            DocumentIntents.DocumentQnA,
            0.7f,
            "Default intent for document interaction."));
    }

    private static bool ContainsAnyKeyword(string prompt, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (prompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuestionPattern(string prompt)
    {
        return prompt.Contains('?') ||
               prompt.StartsWith("what", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("how", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("why", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("when", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("where", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("who", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("which", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("can you", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("could you", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("do you", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("does", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("is", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("are", StringComparison.OrdinalIgnoreCase) ||
               prompt.StartsWith("please", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTabularFiles(IList<ChatInteractionDocumentInfo> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            return false;
        }

        foreach (var doc in documents)
        {
            if (!string.IsNullOrEmpty(doc.FileName) && _tabularFileExtensions.Any(ext => doc.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
