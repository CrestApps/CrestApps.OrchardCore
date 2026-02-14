using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Aggregates tool entries from all registered <see cref="IToolRegistryProvider"/> instances
/// and provides relevance-based search using the shared <see cref="ITextTokenizer"/>.
/// </summary>
internal sealed class DefaultToolRegistry : IToolRegistry
{
    private readonly IEnumerable<IToolRegistryProvider> _providers;
    private readonly ITextTokenizer _tokenizer;
    private readonly ILogger _logger;

    public DefaultToolRegistry(
        IEnumerable<IToolRegistryProvider> providers,
        ITextTokenizer tokenizer,
        ILogger<DefaultToolRegistry> logger)
    {
        _providers = providers;
        _tokenizer = tokenizer;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> GetAllAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        var allEntries = new List<ToolRegistryEntry>();

        foreach (var provider in _providers)
        {
            try
            {
                var entries = await provider.GetToolsAsync(context, cancellationToken);

                if (entries is not null && entries.Count > 0)
                {
                    allEntries.AddRange(entries);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tool registry provider {ProviderType} failed. Skipping.", provider.GetType().Name);
            }
        }

        return allEntries;
    }

    public async Task<IReadOnlyList<ToolRegistryEntry>> SearchAsync(
        string query,
        int topK,
        AICompletionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var allEntries = await GetAllAsync(context, cancellationToken);

        if (allEntries.Count == 0)
        {
            return [];
        }

        var queryTokens = _tokenizer.Tokenize(query);

        if (queryTokens.Count == 0)
        {
            return allEntries.Take(topK).ToList();
        }

        var scored = new List<(ToolRegistryEntry Entry, double Score)>();

        foreach (var entry in allEntries)
        {
            var score = ComputeRelevanceScore(queryTokens, entry);

            scored.Add((entry, score));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .Select(s => s.Entry)
            .ToList();
    }

    private double ComputeRelevanceScore(HashSet<string> queryTokens, ToolRegistryEntry entry)
    {
        var entryTokens = _tokenizer.Tokenize(entry.Name + " " + (entry.Description ?? string.Empty));

        if (entryTokens.Count == 0)
        {
            return 0;
        }

        var matchCount = 0;

        foreach (var queryToken in queryTokens)
        {
            if (entryTokens.Contains(queryToken))
            {
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            return 0;
        }

        // Use max of forward and reverse ratios for better recall.
        // Forward measures how well the query covers the entry;
        // reverse measures how well the entry covers the query.
        var forwardScore = (double)matchCount / queryTokens.Count;
        var reverseScore = (double)matchCount / entryTokens.Count;

        return Math.Max(forwardScore, reverseScore);
    }
}
