using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDocumentProcessingStrategyProvider"/> that routes
/// document processing to the appropriate strategy based on detected intent.
/// </summary>
public sealed class DefaultDocumentProcessingStrategyProvider : IDocumentProcessingStrategyProvider
{
    private readonly IEnumerable<IDocumentProcessingStrategy> _strategies;
    private readonly ILogger<DefaultDocumentProcessingStrategyProvider> _logger;

    public DefaultDocumentProcessingStrategyProvider(
        IEnumerable<IDocumentProcessingStrategy> strategies,
        ILogger<DefaultDocumentProcessingStrategyProvider> logger)
    {
        // Order strategies by their Order property
        _strategies = strategies.OrderBy(s => s.Order);
        _logger = logger;
    }

    /// <inheritdoc />
    public IDocumentProcessingStrategy GetStrategy(DocumentIntent intent)
    {
        foreach (var strategy in _strategies)
        {
            if (strategy.CanHandle(intent))
            {
                _logger.LogDebug("Selected strategy {StrategyType} for intent {Intent}.",
                    strategy.GetType().Name, intent);
                return strategy;
            }
        }

        _logger.LogWarning("No strategy found for intent {Intent}.", intent);
        return null;
    }

    /// <inheritdoc />
    public async Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.IntentResult);

        var strategy = GetStrategy(context.IntentResult.Intent);

        if (strategy == null)
        {
            _logger.LogWarning("No strategy available for intent {Intent}. Returning empty result.",
                context.IntentResult.Intent);
            return DocumentProcessingResult.Empty();
        }

        try
        {
            _logger.LogDebug("Processing documents with strategy {StrategyType} for intent {Intent}.",
                strategy.GetType().Name, context.IntentResult.Intent);

            return await strategy.ProcessAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing documents with strategy {StrategyType}.",
                strategy.GetType().Name);
            return DocumentProcessingResult.Failed($"Error processing documents: {ex.Message}");
        }
    }
}
