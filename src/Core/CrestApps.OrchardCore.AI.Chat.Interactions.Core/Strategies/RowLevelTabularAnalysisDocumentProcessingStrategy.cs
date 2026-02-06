using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling row-by-row tabular data analysis (CSV, Excel, etc.).
/// Uses batched processing to handle large datasets efficiently.
/// Implements caching to avoid re-processing documents on every chat message.
/// </summary>
/// <remarks>
/// This is a heavy processing strategy that can make many API calls (40+ for a 1000-row file).
/// It is only executed when <see cref="ChatInteractionOptions.EnableHeavyProcessingStrategies"/> is true.
/// </remarks>
public sealed class RowLevelTabularAnalysisDocumentProcessingStrategy : DocumentProcessingStrategyBase, IHeavyPromptProcessingStrategy
{
    private static readonly string[] _tabularExtensions = [".csv", ".tsv", ".xlsx", ".xls"];

    private const int BatchProcessingThreshold = 50;

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;
    private readonly ITabularBatchProcessor _batchProcessor;
    private readonly ITabularBatchResultCache _resultCache;
    private readonly RowLevelTabularBatchOptions _settings;
    private readonly ILogger<RowLevelTabularAnalysisDocumentProcessingStrategy> _logger;

    public RowLevelTabularAnalysisDocumentProcessingStrategy(
        IChatInteractionDocumentStore chatInteractionDocumentStore,
        ITabularBatchProcessor batchProcessor,
        ITabularBatchResultCache resultCache,
        IOptions<RowLevelTabularBatchOptions> settings,
        ILogger<RowLevelTabularAnalysisDocumentProcessingStrategy> logger)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
        _batchProcessor = batchProcessor;
        _resultCache = resultCache;
        _settings = settings.Value;
        _logger = logger;
    }

    public override async Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.AnalyzeTabularDataByRow) || !HasDocuments(context))
        {
            return;
        }

        var tabularDocuments = await GetTabularDocumentsAsync(context.Interaction.Documents);

        if (tabularDocuments.Count == 0)
        {
            // Load all documents for fallback
            if (!HasDocumentContent(context))
            {
                var documentIds = context.Interaction.Documents.Select(d => d.DocumentId);
                context.Documents = (await _chatInteractionDocumentStore.GetAsync(documentIds)).ToList();
            }

            var allContent = GetCombinedDocumentText(context);
            context.Result.AddContext(
                allContent,
                "The following is the content of the attached documents for row-level analysis:",
                usedVectorSearch: false);
            return;
        }

        var totalRows = CountTotalDataRows(tabularDocuments);

        if (totalRows <= BatchProcessingThreshold)
        {
            ProcessSmallDataset(context, tabularDocuments);
        }
        else
        {
            await ProcessLargeDatasetAsync(context, tabularDocuments);
        }
    }

    private void ProcessSmallDataset(IntentProcessingContext context, List<ChatInteractionDocument> tabularDocuments)
    {
        var builder = new StringBuilder();
        var processedCount = 0;
        var maxRows = _settings.MaxRowsPerDocument > 0 ? _settings.MaxRowsPerDocument : 250;

        foreach (var doc in tabularDocuments)
        {
            if (string.IsNullOrWhiteSpace(doc.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }

            builder.AppendLine($"[Tabular data from: {doc.FileName ?? "Unknown file"}]");
            builder.AppendLine(LimitTabularRows(doc.Text, maxRows));
            processedCount++;
        }

        if (builder.Length == 0)
        {
            context.Result.AddContext(
                GetDocumentMetadata(context),
                "Tabular files are attached but could not be read:",
                usedVectorSearch: false);
            return;
        }

        context.Result.AddContext(
            GetRowLevelInstructions(context.Prompt),
            "Follow these instructions when responding:",
            usedVectorSearch: false);

        var prefix = processedCount == 1
            ? "The following is tabular data for row-by-row processing. Treat each data row as an independent record and preserve verbatim quotes when requested:"
            : $"The following is tabular data from {processedCount} attached files for row-by-row processing:";

        context.Result.AddContext(builder.ToString(), prefix, usedVectorSearch: false);
    }

    private async Task ProcessLargeDatasetAsync(IntentProcessingContext context, List<ChatInteractionDocument> tabularDocuments)
    {
        var interactionId = context.Interaction.ItemId;
        var prompt = context.Prompt;

        // Check cache first if enabled
        if (_settings.EnableResultCaching)
        {
            var cachedResult = TryGetCachedResult(interactionId, tabularDocuments, prompt);
            if (cachedResult is not null)
            {
                _logger.LogInformation(
                    "Using cached batch results for interaction {InteractionId}. Batches: {BatchCount}, Rows: {RowCount}",
                    interactionId, cachedResult.TotalBatches, cachedResult.TotalRowsProcessed);

                AddCachedResultToContext(context, cachedResult);
                return;
            }
        }

        _logger.LogInformation(
            "Processing large tabular dataset with batched execution. Documents: {DocCount}",
            tabularDocuments.Count);

        var allBatches = new List<TabularBatch>();
        var fileNames = new List<string>();

        foreach (var doc in tabularDocuments)
        {
            if (string.IsNullOrWhiteSpace(doc.Text))
            {
                continue;
            }

            var batches = _batchProcessor.SplitIntoBatches(doc.Text, doc.FileName ?? "Unknown file");

            if (batches.Count > 0)
            {
                allBatches.AddRange(batches);
                fileNames.Add(doc.FileName ?? "Unknown file");
            }
        }

        if (allBatches.Count == 0)
        {
            context.Result.AddContext(
                GetDocumentMetadata(context),
                "Tabular files are attached but could not be read:",
                usedVectorSearch: false);
            return;
        }

        var results = await _batchProcessor.ProcessBatchesAsync(allBatches, prompt, context);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count - successCount;
        var totalRowsProcessed = results.Where(r => r.Success).Sum(r => r.ProcessedRowCount);

        _logger.LogInformation(
            "Batch processing complete. Successful: {SuccessCount}/{TotalCount}, Rows: {RowCount}",
            successCount, results.Count, totalRowsProcessed);

        var mergedOutput = _batchProcessor.MergeResults(results, includeHeader: false);

        if (string.IsNullOrWhiteSpace(mergedOutput))
        {
            context.Result.SetFailed("Failed to process tabular data. All batches returned errors.");
            return;
        }

        // Cache the results for future requests
        if (_settings.EnableResultCaching && successCount > 0)
        {
            CacheResults(interactionId, tabularDocuments, prompt, results, mergedOutput, fileNames, successCount, totalRowsProcessed);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Row-level analysis results from {fileNames.Count} file(s):");
        builder.AppendLine($"- Total batches: {allBatches.Count}");
        builder.AppendLine($"- Successful batches: {successCount}");
        builder.AppendLine($"- Total rows processed: {totalRowsProcessed}");

        if (failureCount > 0)
        {
            builder.AppendLine($"- Failed batches: {failureCount} (see errors below)");
        }

        builder.AppendLine();
        builder.AppendLine("--- RESULTS ---");
        builder.AppendLine();
        builder.Append(mergedOutput);

        context.Result.AddContext(
            builder.ToString(),
            "The following is the result of row-by-row analysis of the attached tabular data:",
            usedVectorSearch: false);
    }

    private TabularBatchCacheEntry TryGetCachedResult(
        string interactionId,
        List<ChatInteractionDocument> documents,
        string prompt)
    {
        try
        {
            var documentData = documents
                .Where(d => !string.IsNullOrWhiteSpace(d.Text))
                .Select(d => (d.FileName ?? "unknown", d.Text));

            var contentHash = _resultCache.ComputeDocumentContentHash(documentData);
            var cacheKey = _resultCache.GenerateCacheKey(interactionId, contentHash, prompt);

            return _resultCache.TryGet(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking cache for batch results.");
            return null;
        }
    }

    private void CacheResults(
        string interactionId,
        List<ChatInteractionDocument> documents,
        string prompt,
        IList<TabularBatchResult> results,
        string mergedOutput,
        List<string> fileNames,
        int successCount,
        int totalRowsProcessed)
    {
        try
        {
            var documentData = documents
                .Where(d => !string.IsNullOrWhiteSpace(d.Text))
                .Select(d => (d.FileName ?? "unknown", d.Text));

            var contentHash = _resultCache.ComputeDocumentContentHash(documentData);
            var cacheKey = _resultCache.GenerateCacheKey(interactionId, contentHash, prompt);

            var cacheEntry = new TabularBatchCacheEntry
            {
                Results = results.ToList(),
                MergedOutput = mergedOutput,
                TotalBatches = results.Count,
                SuccessfulBatches = successCount,
                TotalRowsProcessed = totalRowsProcessed,
                CreatedUtc = DateTime.UtcNow,
                Prompt = prompt,
                FileNames = fileNames,
            };

            _resultCache.Set(cacheKey, cacheEntry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching batch results.");
        }
    }

    private static void AddCachedResultToContext(IntentProcessingContext context, TabularBatchCacheEntry cached)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Row-level analysis results from {cached.FileNames?.Count ?? 0} file(s) (cached):");
        builder.AppendLine($"- Total batches: {cached.TotalBatches}");
        builder.AppendLine($"- Successful batches: {cached.SuccessfulBatches}");
        builder.AppendLine($"- Total rows processed: {cached.TotalRowsProcessed}");
        builder.AppendLine($"- Cached at: {cached.CreatedUtc:u}");
        builder.AppendLine();
        builder.AppendLine("--- RESULTS ---");
        builder.AppendLine();
        builder.Append(cached.MergedOutput);

        context.Result.AddContext(
            builder.ToString(),
            "The following is the result of row-by-row analysis of the attached tabular data (from cache):",
            usedVectorSearch: false);
    }

    private async Task<List<ChatInteractionDocument>> GetTabularDocumentsAsync(IList<ChatInteractionDocumentInfo> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            return [];
        }

        var tabularDocumentIds = documents.Where(doc => IsTabularFile(doc.FileName)).Select(doc => doc.DocumentId);

        return (await _chatInteractionDocumentStore.GetAsync(tabularDocumentIds)).ToList();
    }

    private static int CountTotalDataRows(List<ChatInteractionDocument> documents)
    {
        var totalRows = 0;
        foreach (var doc in documents)
        {
            if (!string.IsNullOrWhiteSpace(doc.Text))
            {
                totalRows += Math.Max(0, doc.Text.Count(c => c == '\n'));
            }
        }
        return totalRows;
    }

    private static string GetRowLevelInstructions(string prompt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are performing row-level analysis over tabular data.");
        builder.AppendLine("1) Treat the first row as headers.");
        builder.AppendLine("2) Process each subsequent row independently.");
        builder.AppendLine("3) Output exactly one result per input row.");
        builder.AppendLine("4) Preserve verbatim excerpts when the prompt asks for exact quotes.");
        builder.AppendLine("5) If the requested item does not exist in a row, output \"No Escalation found\" (or equivalent explicitly requested by the user).");
        builder.AppendLine("6) Keep output in a compact tabular format aligned to the input (e.g., CSV/Markdown table). Include a stable row identifier when available (e.g., Eureka ID).");

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            builder.AppendLine();
            builder.AppendLine("User request:");
            builder.AppendLine(prompt);
        }

        return builder.ToString();
    }

    private static bool IsTabularFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        foreach (var ext in _tabularExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string LimitTabularRows(string content, int maxRows)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var lines = content.Split('\n');
        if (lines.Length <= maxRows + 1)
        {
            return content;
        }

        var limitedLines = new string[maxRows + 2];
        for (var i = 0; i <= maxRows && i < lines.Length; i++)
        {
            limitedLines[i] = lines[i];
        }

        limitedLines[maxRows + 1] = $"... (truncated, showing first {maxRows} of {lines.Length - 1} data rows)";
        return string.Join('\n', limitedLines);
    }
}
