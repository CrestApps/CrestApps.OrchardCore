using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Default implementation of <see cref="IMcpCapabilityResolver"/> that uses a hybrid
/// approach combining embedding-based semantic similarity and keyword/token overlap
/// matching to find MCP capabilities relevant to a user prompt.
/// </summary>
/// <remarks>
/// Resolution strategy (in priority order):
/// <list type="number">
///   <item>If total capabilities ≤ <see cref="McpCapabilityResolverOptions.IncludeAllThreshold"/>,
///         return all (small set optimization).</item>
///   <item>Hybrid matching: run both embedding-based semantic similarity (if available) and
///         keyword/token overlap matching in parallel, then merge results keeping the highest
///         score per capability. This maximizes recall — embeddings catch semantic similarity
///         while keywords catch exact lexical matches (and vice versa).</item>
/// </list>
///
/// <para><b>Tokenization</b> uses a Lucene.NET analyzer pipeline optimized for code identifiers:
/// WhitespaceTokenizer → WordDelimiterFilter (camelCase splitting) → LowerCaseFilter →
/// StopFilter (English) → PorterStemFilter. A regex pre-processing step handles consecutive
/// uppercase sequences (e.g., "JSONSchema" → "JSON Schema") to work around a known
/// Lucene 4.x WordDelimiterFilter limitation.</para>
///
/// <para><b>Embedding search</b> uses pre-normalized vectors so cosine similarity reduces
/// to a dot product, avoiding magnitude computation at query time.</para>
/// </remarks>
internal sealed class DefaultMcpCapabilityResolver : IMcpCapabilityResolver
{
    private const LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;

    // Inserts a space between consecutive uppercase sequences and the start of a new word.
    // Handles a known limitation of Lucene 4.x WordDelimiterFilter where UPPER→letter
    // transitions don't trigger splits (e.g., "JSONSchema" → "JSON Schema").
    private static readonly Regex _consecutiveUppercasePattern = new(@"(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

    // Shared analyzer instance. Lucene.NET analyzers are thread-safe for GetTokenStream()
    // (the default reuse strategy uses per-thread TokenStream pooling via CloseableThreadLocal).
    private static readonly CapabilityAnalyzer _sharedAnalyzer = new();

    private readonly ISourceCatalog<McpConnection> _store;
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly IMcpCapabilityEmbeddingCacheProvider _embeddingCache;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly AIProviderOptions _providerOptions;
    private readonly McpCapabilityResolverOptions _resolverOptions;
    private readonly ILogger _logger;

    public DefaultMcpCapabilityResolver(
        ISourceCatalog<McpConnection> store,
        IMcpServerMetadataCacheProvider metadataProvider,
        IMcpCapabilityEmbeddingCacheProvider embeddingCache,
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<McpCapabilityResolverOptions> resolverOptions,
        ILogger<DefaultMcpCapabilityResolver> logger)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _embeddingCache = embeddingCache;
        _aiClientFactory = aiClientFactory;
        _providerOptions = providerOptions.Value;
        _resolverOptions = resolverOptions.Value;
        _logger = logger;
    }

    public async Task<McpCapabilityResolutionResult> ResolveAsync(
        string prompt,
        string providerName,
        string connectionName,
        string[] mcpConnectionIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt) || mcpConnectionIds is null || mcpConnectionIds.Length == 0)
        {
            return McpCapabilityResolutionResult.Empty;
        }

        try
        {
            // Resolve configured MCP connections.
            var connections = await _store.GetAsync(mcpConnectionIds);

            if (connections.Count == 0)
            {
                return McpCapabilityResolutionResult.Empty;
            }

            // Fetch capabilities from cache.
            var capabilitiesList = new List<McpServerCapabilities>();

            foreach (var connection in connections)
            {
                try
                {
                    var capabilities = await _metadataProvider.GetCapabilitiesAsync(connection);

                    if (capabilities is not null)
                    {
                        capabilitiesList.Add(capabilities);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to get capabilities from MCP connection '{ConnectionId}' during pre-intent resolution.",
                        connection.ItemId);
                }
            }

            if (capabilitiesList.Count == 0)
            {
                return McpCapabilityResolutionResult.Empty;
            }

            // Build a flat list of all capability entries for scoring.
            var entries = BuildCapabilityEntries(capabilitiesList);

            if (entries.Count == 0)
            {
                return McpCapabilityResolutionResult.Empty;
            }

            // Strategy 1: If total capabilities are small, return all.
            if (entries.Count <= _resolverOptions.IncludeAllThreshold)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Capability count ({Count}) is within include-all threshold ({Threshold}). Returning all capabilities.",
                        entries.Count, _resolverOptions.IncludeAllThreshold);
                }

                return BuildResult(entries, score: 1.0f);
            }

            // Strategy 2: Hybrid matching — run both embedding-based and keyword-based
            // matching, then merge results keeping the best score per capability.
            var embeddingCandidates = await TryEmbeddingMatchAsync(
                prompt, providerName, connectionName, capabilitiesList, entries, cancellationToken);

            var keywordCandidates = KeywordMatch(prompt, entries);

            var mergedCandidates = MergeCandidates(embeddingCandidates, keywordCandidates);

            if (mergedCandidates.Count > 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Hybrid resolution found {Count} candidate(s) (embedding: {EmbeddingCount}, keyword: {KeywordCount}).",
                        mergedCandidates.Count,
                        embeddingCandidates?.Count ?? 0,
                        keywordCandidates?.Count ?? 0);
                }

                return new McpCapabilityResolutionResult(mergedCandidates);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No capabilities matched user prompt via embedding or keyword strategies.");
            }

            return McpCapabilityResolutionResult.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP capability resolution failed. Continuing without pre-resolved capabilities.");

            return McpCapabilityResolutionResult.Empty;
        }
    }

    /// <summary>
    /// Attempts embedding-based semantic matching using pre-normalized vectors.
    /// When vectors are pre-normalized (L2 norm = 1), cosine similarity reduces to
    /// a simple dot product, avoiding magnitude computation at query time.
    /// Returns null if no embedding model is available.
    /// </summary>
    private async Task<List<McpCapabilityCandidate>> TryEmbeddingMatchAsync(
        string prompt,
        string providerName,
        string connectionName,
        List<McpServerCapabilities> capabilitiesList,
        List<CapabilityEntry> entries,
        CancellationToken cancellationToken)
    {
        var embeddingGenerator = await CreateEmbeddingGeneratorAsync(providerName, connectionName);

        if (embeddingGenerator is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("No embedding generator available. Falling back to keyword matching.");
            }

            return null;
        }

        // Get or create capability embeddings (pre-normalized at cache time).
        var capabilityEmbeddings = await _embeddingCache.GetOrCreateEmbeddingsAsync(
            capabilitiesList, embeddingGenerator, cancellationToken);

        if (capabilityEmbeddings.Count == 0)
        {
            return null;
        }

        // Embed the user prompt and normalize.
        var promptEmbeddings = await embeddingGenerator.GenerateAsync([prompt], cancellationToken: cancellationToken);

        if (promptEmbeddings is null || promptEmbeddings.Count == 0 ||
            promptEmbeddings[0].Vector.Length == 0)
        {
            _logger.LogWarning("Failed to generate embedding for user prompt during capability resolution.");

            return null;
        }

        var promptVector = NormalizeL2(promptEmbeddings[0].Vector.ToArray());

        // Since both vectors are pre-normalized, cosine similarity = dot product.
        var candidates = new List<McpCapabilityCandidate>();

        foreach (var embedding in capabilityEmbeddings)
        {
            var similarity = DotProduct(promptVector, embedding.Embedding);

            if (similarity >= _resolverOptions.SimilarityThreshold)
            {
                candidates.Add(new McpCapabilityCandidate
                {
                    ConnectionId = embedding.ConnectionId,
                    ConnectionDisplayText = embedding.ConnectionDisplayText,
                    CapabilityName = embedding.CapabilityName,
                    CapabilityDescription = embedding.CapabilityDescription,
                    CapabilityType = embedding.CapabilityType,
                    SimilarityScore = similarity,
                });
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Embedding-based matching found {Count} candidate(s) above threshold {Threshold}.",
                candidates.Count, _resolverOptions.SimilarityThreshold);
        }

        return candidates;
    }

    /// <summary>
    /// Keyword/token overlap matching. Tokenizes the prompt and each capability text
    /// using the Lucene.NET analyzer pipeline (WhitespaceTokenizer + WordDelimiterFilter +
    /// LowerCaseFilter + StopFilter + PorterStemFilter), then scores using the maximum
    /// of forward and reverse token overlap ratios for better recall.
    /// </summary>
    private List<McpCapabilityCandidate> KeywordMatch(string prompt, List<CapabilityEntry> entries)
    {
        var promptTokens = Tokenize(prompt);

        if (promptTokens.Count == 0)
        {
            return null;
        }

        var candidates = new List<McpCapabilityCandidate>();

        foreach (var entry in entries)
        {
            var capabilityTokens = Tokenize(entry.Text);

            if (capabilityTokens.Count == 0)
            {
                continue;
            }

            // Count prompt tokens that appear in the capability text.
            var matchCount = 0;

            foreach (var token in promptTokens)
            {
                if (capabilityTokens.Contains(token))
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                continue;
            }

            // Use max of forward and reverse ratios. Forward measures how well the prompt
            // covers the capability; reverse measures how well the capability covers the prompt.
            // This ensures short capability names (e.g., "recipe-schema") score well against
            // longer prompts even when the forward ratio is diluted by many prompt tokens.
            var forwardScore = (float)matchCount / promptTokens.Count;
            var reverseScore = (float)matchCount / capabilityTokens.Count;
            var score = Math.Max(forwardScore, reverseScore);

            if (score >= _resolverOptions.KeywordMatchThreshold)
            {
                candidates.Add(new McpCapabilityCandidate
                {
                    ConnectionId = entry.ConnectionId,
                    ConnectionDisplayText = entry.ConnectionDisplayText,
                    CapabilityName = entry.Name,
                    CapabilityDescription = entry.Description,
                    CapabilityType = entry.Type,
                    SimilarityScore = score,
                });
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Keyword-based matching found {Count} candidate(s) above threshold {Threshold}.",
                candidates.Count, _resolverOptions.KeywordMatchThreshold);
        }

        return candidates;
    }

    /// <summary>
    /// Merges candidates from multiple strategies, deduplicating by (ConnectionId, CapabilityName)
    /// and keeping the highest score per capability. Sorts by descending score and applies TopK.
    /// </summary>
    private List<McpCapabilityCandidate> MergeCandidates(
        List<McpCapabilityCandidate> embeddingCandidates,
        List<McpCapabilityCandidate> keywordCandidates)
    {
        var map = new Dictionary<string, McpCapabilityCandidate>(StringComparer.OrdinalIgnoreCase);

        AddToMap(map, embeddingCandidates);
        AddToMap(map, keywordCandidates);

        if (map.Count == 0)
        {
            return [];
        }

        var result = map.Values.ToList();
        result.Sort((a, b) => b.SimilarityScore.CompareTo(a.SimilarityScore));

        if (result.Count > _resolverOptions.TopK)
        {
            result.RemoveRange(_resolverOptions.TopK, result.Count - _resolverOptions.TopK);
        }

        return result;

        static void AddToMap(
            Dictionary<string, McpCapabilityCandidate> map,
            List<McpCapabilityCandidate> candidates)
        {
            if (candidates is null)
            {
                return;
            }

            foreach (var candidate in candidates)
            {
                var key = $"{candidate.ConnectionId}\0{candidate.CapabilityName}";

                if (!map.TryGetValue(key, out var existing) ||
                    candidate.SimilarityScore > existing.SimilarityScore)
                {
                    map[key] = candidate;
                }
            }
        }
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(
        string providerName, string connectionName)
    {
        if (string.IsNullOrEmpty(providerName) ||
            !_providerOptions.Providers.TryGetValue(providerName, out var provider))
        {
            return null;
        }

        if (string.IsNullOrEmpty(connectionName))
        {
            connectionName = provider.DefaultConnectionName;
        }

        if (string.IsNullOrEmpty(connectionName) ||
            !provider.Connections.TryGetValue(connectionName, out var connection))
        {
            return null;
        }

        var deploymentName = connection.GetDefaultEmbeddingDeploymentName(throwException: false);

        if (string.IsNullOrEmpty(deploymentName))
        {
            return null;
        }

        return await _aiClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);
    }

    /// <summary>
    /// Builds a flat list of capability entries from all connections for scoring.
    /// </summary>
    private static List<CapabilityEntry> BuildCapabilityEntries(List<McpServerCapabilities> capabilitiesList)
    {
        var entries = new List<CapabilityEntry>();

        foreach (var server in capabilitiesList)
        {
            AddEntries(entries, server, server.Tools, McpCapabilityType.Tool);
            AddEntries(entries, server, server.Prompts, McpCapabilityType.Prompt);
            AddEntries(entries, server, server.Resources, McpCapabilityType.Resource);
            AddEntries(entries, server, server.ResourceTemplates, McpCapabilityType.ResourceTemplate);
        }

        return entries;

        static void AddEntries(
            List<CapabilityEntry> entries,
            McpServerCapabilities server,
            IReadOnlyList<McpServerCapability> items,
            McpCapabilityType type)
        {
            if (items is null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                // Include the URI or URI template in the searchable text for better keyword matching.
                var uriText = item.UriTemplate ?? item.Uri;
                var parts = new List<string>(3) { item.Name };

                if (!string.IsNullOrWhiteSpace(uriText))
                {
                    parts.Add(uriText);
                }

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    parts.Add(item.Description);
                }

                entries.Add(new CapabilityEntry
                {
                    ConnectionId = server.ConnectionId,
                    ConnectionDisplayText = server.ConnectionDisplayText,
                    Name = item.Name,
                    Description = item.Description ?? string.Empty,
                    Type = type,
                    Text = string.Join(": ", parts),
                });
            }
        }
    }

    /// <summary>
    /// Builds a result that includes all given entries with the specified score.
    /// </summary>
    private static McpCapabilityResolutionResult BuildResult(List<CapabilityEntry> entries, float score)
    {
        var candidates = new List<McpCapabilityCandidate>(entries.Count);

        foreach (var entry in entries)
        {
            candidates.Add(new McpCapabilityCandidate
            {
                ConnectionId = entry.ConnectionId,
                ConnectionDisplayText = entry.ConnectionDisplayText,
                CapabilityName = entry.Name,
                CapabilityDescription = entry.Description,
                CapabilityType = entry.Type,
                SimilarityScore = score,
            });
        }

        return new McpCapabilityResolutionResult(candidates);
    }

    /// <summary>
    /// Tokenizes text using a shared Lucene.NET analyzer pipeline:
    /// WhitespaceTokenizer → WordDelimiterFilter (camelCase/PascalCase splitting) →
    /// LowerCaseFilter → StopFilter (English stop words) → PorterStemFilter.
    /// A regex pre-processing step handles consecutive uppercase sequences
    /// (e.g., "JSONSchema" → "JSON Schema") for Lucene 4.x compatibility.
    /// </summary>
    internal static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Pre-process: split consecutive uppercase sequences before a new word
        // (e.g., "JSONSchema" → "JSON Schema", "MCPServer" → "MCP Server").
        // This works around a Lucene 4.x WordDelimiterFilter limitation where
        // UPPER→letter transitions are not treated as split points.
        text = _consecutiveUppercasePattern.Replace(text, " ");

        var tokens = new HashSet<string>(StringComparer.Ordinal);

        using var tokenStream = _sharedAnalyzer.GetTokenStream("capability", text);

        var charTermAttr = tokenStream.AddAttribute<ICharTermAttribute>();
        tokenStream.Reset();

        while (tokenStream.IncrementToken())
        {
            var token = charTermAttr.ToString();

            if (token.Length > 0)
            {
                tokens.Add(token);
            }
        }

        tokenStream.End();

        return tokens;
    }

    /// <summary>
    /// Normalizes a vector to unit length (L2 norm = 1). When both vectors in a
    /// similarity comparison are pre-normalized, cosine similarity reduces to a
    /// simple dot product, avoiding redundant magnitude computation.
    /// </summary>
    internal static float[] NormalizeL2(float[] vector)
    {
        var sumOfSquares = 0f;

        for (var i = 0; i < vector.Length; i++)
        {
            sumOfSquares += vector[i] * vector[i];
        }

        var magnitude = MathF.Sqrt(sumOfSquares);

        if (magnitude == 0f)
        {
            return vector;
        }

        var normalized = new float[vector.Length];

        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }

    /// <summary>
    /// Computes the dot product of two vectors. When both vectors are pre-normalized
    /// (L2 norm = 1), this is equivalent to cosine similarity.
    /// </summary>
    internal static float DotProduct(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length || vectorA.Length == 0)
        {
            return 0f;
        }

        var result = 0f;

        for (var i = 0; i < vectorA.Length; i++)
        {
            result += vectorA[i] * vectorB[i];
        }

        return result;
    }

    private struct CapabilityEntry
    {
        public string ConnectionId;
        public string ConnectionDisplayText;
        public string Name;
        public string Description;
        public McpCapabilityType Type;
        public string Text;
    }

    /// <summary>
    /// Custom Lucene.NET analyzer for MCP capability matching. Applies:
    /// <list type="number">
    ///   <item>WhitespaceTokenizer — splits on whitespace, preserving case information
    ///         for downstream filters. Recommended over StandardTokenizer when using
    ///         WordDelimiterFilter (per Lucene documentation).</item>
    ///   <item>WordDelimiterFilter — splits camelCase/PascalCase identifiers
    ///         (e.g., "getRecipeSchema" → "get", "Recipe", "Schema").</item>
    ///   <item>LowerCaseFilter — normalizes to lowercase.</item>
    ///   <item>StopFilter — removes common English stop words using the
    ///         <see cref="EnglishAnalyzer.DefaultStopSet"/>.</item>
    ///   <item>PorterStemFilter — applies Porter stemming for morphological normalization
    ///         (e.g., "recipes" → "recip", "enabling" → "enabl").</item>
    /// </list>
    /// <para>Thread-safe: Lucene.NET analyzers use per-thread TokenStream pooling.</para>
    /// </summary>
    private sealed class CapabilityAnalyzer : Analyzer
    {
        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var tokenizer = new WhitespaceTokenizer(_luceneVersion, reader);

            TokenStream filter = new WordDelimiterFilter(
                _luceneVersion,
                tokenizer,
                WordDelimiterFlags.GENERATE_WORD_PARTS
                | WordDelimiterFlags.GENERATE_NUMBER_PARTS
                | WordDelimiterFlags.SPLIT_ON_CASE_CHANGE
                | WordDelimiterFlags.SPLIT_ON_NUMERICS
                | WordDelimiterFlags.STEM_ENGLISH_POSSESSIVE,
                CharArraySet.Empty);

            filter = new LowerCaseFilter(_luceneVersion, filter);
            filter = new StopFilter(_luceneVersion, filter, EnglishAnalyzer.DefaultStopSet);
            filter = new PorterStemFilter(filter);

            return new TokenStreamComponents(tokenizer, filter);
        }
    }
}
