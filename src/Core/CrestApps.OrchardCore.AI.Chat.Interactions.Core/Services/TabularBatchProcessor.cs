using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="ITabularBatchProcessor"/> that splits tabular data into batches,
/// processes them with bounded concurrency using LLM, and merges results deterministically.
/// </summary>
public sealed class TabularBatchProcessor : ITabularBatchProcessor
{
    private readonly IAICompletionService _completionService;
    private readonly IAITemplateService _aiTemplateService;
    private readonly RowLevelTabularBatchOptions _settings;
    private readonly ILogger<TabularBatchProcessor> _logger;

    public TabularBatchProcessor(
        IAICompletionService completionService,
        IAITemplateService aiTemplateService,
        IOptions<RowLevelTabularBatchOptions> settings,
        ILogger<TabularBatchProcessor> logger)
    {
        _completionService = completionService;
        _aiTemplateService = aiTemplateService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public IList<TabularBatch> SplitIntoBatches(string content, string fileName)
    {
        var batches = new List<TabularBatch>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return batches;
        }

        var lines = content.Split('\n', StringSplitOptions.None);

        if (lines.Length == 0)
        {
            return batches;
        }

        // First line is always the header
        var headerRow = lines[0];
        var dataLines = lines.Skip(1).ToList();

        // Apply max rows limit
        var maxRows = _settings.MaxRowsPerDocument;
        if (maxRows > 0 && dataLines.Count > maxRows)
        {
            _logger.LogWarning(
                "Document '{FileName}' has {ActualRows} rows, exceeding the maximum of {MaxRows}. Truncating.",
                fileName, dataLines.Count, maxRows);
            dataLines = dataLines.Take(maxRows).ToList();
        }

        if (dataLines.Count == 0)
        {
            return batches;
        }

        var batchSize = _settings.RowBatchSize;
        if (batchSize <= 0)
        {
            batchSize = 25; // Default fallback
        }

        var batchIndex = 0;
        for (var i = 0; i < dataLines.Count; i += batchSize)
        {
            var batchRows = dataLines.Skip(i).Take(batchSize).ToList();

            batches.Add(new TabularBatch
            {
                BatchIndex = batchIndex,
                FileName = fileName,
                HeaderRow = headerRow,
                DataRows = batchRows,
                RowStartIndex = i + 1, // 1-based index
                RowEndIndex = i + batchRows.Count, // 1-based index
            });

            batchIndex++;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Split document '{FileName}' into {BatchCount} batches of up to {BatchSize} rows each.",
                fileName, batches.Count, batchSize);
        }

        return batches;
    }

    /// <inheritdoc />
    public async Task<IList<TabularBatchResult>> ProcessBatchesAsync(
        IList<TabularBatch> batches,
        string userPrompt,
        TabularBatchContext context,
        CancellationToken cancellationToken = default)
    {
        if (batches is null || batches.Count == 0)
        {
            return [];
        }

        var results = new TabularBatchResult[batches.Count];
        var maxConcurrency = Math.Max(1, _settings.MaxConcurrentBatches);
        var continueOnFailure = _settings.ContinueOnBatchFailure;
        var delayBetweenBatches = _settings.DelayBetweenBatchesMs;

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var failureOccurred = false;
        var processedCount = 0;

        var tasks = batches.Select(async batch =>
        {
            // Check if we should stop due to a previous failure
            if (!continueOnFailure && failureOccurred)
            {
                results[batch.BatchIndex] = TabularBatchResult.CreateFailure(
                    batch.BatchIndex,
                    batch.RowStartIndex,
                    batch.RowEndIndex,
                    batch.RowCount,
                    "Processing stopped due to previous batch failure.");
                return;
            }

            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Check again after acquiring semaphore
                if (!continueOnFailure && failureOccurred)
                {
                    results[batch.BatchIndex] = TabularBatchResult.CreateFailure(
                        batch.BatchIndex,
                        batch.RowStartIndex,
                        batch.RowEndIndex,
                        batch.RowCount,
                        "Processing stopped due to previous batch failure.");
                    return;
                }

                // Add delay between batch submissions to avoid rate limiting
                if (delayBetweenBatches > 0 && Interlocked.Increment(ref processedCount) > 1)
                {
                    await Task.Delay(delayBetweenBatches, cancellationToken);
                }

                var result = await ProcessSingleBatchAsync(batch, userPrompt, context, cancellationToken);
                results[batch.BatchIndex] = result;

                if (!result.Success)
                {
                    failureOccurred = true;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        return results.ToList();
    }

    /// <inheritdoc />
    public string MergeResults(IList<TabularBatchResult> results, bool includeHeader = true)
    {
        if (results is null || results.Count == 0)
        {
            return string.Empty;
        }

        var builder = ZString.CreateStringBuilder();
        var sortedResults = results.OrderBy(r => r.BatchIndex).ToList();

        var hasSuccessfulResults = false;
        var failedBatches = new List<TabularBatchResult>();

        foreach (var result in sortedResults)
        {
            if (result.Success && !string.IsNullOrWhiteSpace(result.OutputContent))
            {
                if (hasSuccessfulResults)
                {
                    // Add newline between batch outputs
                    builder.AppendLine();
                }

                builder.Append(result.OutputContent.Trim());
                hasSuccessfulResults = true;
            }
            else if (!result.Success)
            {
                failedBatches.Add(result);
            }
        }

        // Append error summary if there were failures
        if (failedBatches.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("---");
            builder.AppendLine("Processing Errors:");

            foreach (var failed in failedBatches)
            {
                builder.Append("- Rows ");
                builder.Append(failed.RowStartIndex);
                builder.Append('-');
                builder.Append(failed.RowEndIndex);
                builder.Append(": ");
                builder.AppendLine(failed.ErrorMessage ?? "Unknown error");
            }
        }

        return builder.ToString();
    }

    private async Task<TabularBatchResult> ProcessSingleBatchAsync(
        TabularBatch batch,
        string userPrompt,
        TabularBatchContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceContext = context.CompletionContext;
            if (sourceContext is null)
            {
                return TabularBatchResult.CreateFailure(
                    batch.BatchIndex,
                    batch.RowStartIndex,
                    batch.RowEndIndex,
                    batch.RowCount,
                    "Completion context is not available.");
            }

            // Build the batch-specific prompt with instructions
            var batchPrompt = BuildBatchPrompt(batch, userPrompt);

            // Create completion context for this batch
            var completionContext = new AICompletionContext
            {
                ConnectionName = sourceContext.ConnectionName,
                DeploymentId = sourceContext.DeploymentId,
                SystemMessage = await GetBatchSystemMessageAsync(batch, sourceContext.SystemMessage),
                Temperature = sourceContext.Temperature ?? 0.1f, // Use low temperature for consistent row processing
                TopP = sourceContext.TopP ?? 1.0f,
                FrequencyPenalty = sourceContext.FrequencyPenalty,
                PresencePenalty = sourceContext.PresencePenalty,
                MaxTokens = sourceContext.MaxTokens,
                UserMarkdownInResponse = false, // We want structured output
                DisableTools = true, // Disable tools for batch processing
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.BatchTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var response = await _completionService.CompleteAsync(
                context.Source,
                [new ChatMessage(ChatRole.User, batchPrompt)],
                completionContext,
                linkedCts.Token);

            var outputText = response?.Messages?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(outputText))
            {
                return TabularBatchResult.CreateFailure(
                    batch.BatchIndex,
                    batch.RowStartIndex,
                    batch.RowEndIndex,
                    batch.RowCount,
                    "LLM returned empty response.");
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Successfully processed batch {BatchIndex} (rows {StartRow}-{EndRow}) from '{FileName}'.",
                    batch.BatchIndex, batch.RowStartIndex, batch.RowEndIndex, batch.FileName);
            }

            return TabularBatchResult.CreateSuccess(
                batch.BatchIndex,
                batch.RowStartIndex,
                batch.RowEndIndex,
                batch.RowCount,
                outputText);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TabularBatchResult.CreateFailure(
                batch.BatchIndex,
                batch.RowStartIndex,
                batch.RowEndIndex,
                batch.RowCount,
                "Processing was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return TabularBatchResult.CreateFailure(
                batch.BatchIndex,
                batch.RowStartIndex,
                batch.RowEndIndex,
                batch.RowCount,
                $"Batch processing timed out after {_settings.BatchTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing batch {BatchIndex} (rows {StartRow}-{EndRow}) from '{FileName}'.",
                batch.BatchIndex, batch.RowStartIndex, batch.RowEndIndex, batch.FileName);

            return TabularBatchResult.CreateFailure(
                batch.BatchIndex,
                batch.RowStartIndex,
                batch.RowEndIndex,
                batch.RowCount,
                $"An error occurred while processing the batch.");
        }
    }

    private static string BuildBatchPrompt(TabularBatch batch, string userPrompt)
    {
        using var builder = ZString.CreateStringBuilder();

        builder.AppendLine("Process the following tabular data rows according to the instructions below.");
        builder.AppendLine();
        builder.Append("This is batch ");
        builder.Append(batch.BatchIndex + 1);
        builder.Append(", containing rows ");
        builder.Append(batch.RowStartIndex);
        builder.Append(" through ");
        builder.Append(batch.RowEndIndex);
        builder.Append(" from file: ");
        builder.AppendLine(batch.FileName ?? "Unknown");
        builder.AppendLine();
        builder.AppendLine("--- DATA START ---");
        builder.AppendLine(batch.GetContent());
        builder.AppendLine("--- DATA END ---");
        builder.AppendLine();
        builder.AppendLine("User Instructions:");
        builder.AppendLine(userPrompt);

        return builder.ToString();
    }

    private async Task<string> GetBatchSystemMessageAsync(TabularBatch batch, string baseSystemMessage)
    {
        var arguments = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(baseSystemMessage))
        {
            arguments["baseSystemMessage"] = baseSystemMessage;
        }

        return await _aiTemplateService.RenderAsync(AITemplateIds.TabularBatchProcessing, arguments);
    }
}
