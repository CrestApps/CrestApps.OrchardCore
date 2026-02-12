using System.Text.RegularExpressions;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Tokenizes text using a Lucene.NET analyzer pipeline optimized for code identifiers
/// and natural language matching.
/// </summary>
/// <remarks>
/// <para>Pipeline: WhitespaceTokenizer → WordDelimiterFilter (camelCase splitting) →
/// LowerCaseFilter → StopFilter (English) → PorterStemFilter.</para>
/// <para>A regex pre-processing step handles consecutive uppercase sequences
/// (e.g., "JSONSchema" → "JSON Schema") to work around a known Lucene 4.x
/// WordDelimiterFilter limitation.</para>
/// <para>Thread-safe: uses a shared <see cref="Analyzer"/> instance with per-thread
/// TokenStream pooling.</para>
/// </remarks>
public sealed class LuceneTextTokenizer : ITextTokenizer
{
    private const LuceneVersion _luceneVersion = LuceneVersion.LUCENE_48;

    // Inserts a space between consecutive uppercase sequences and the start of a new word.
    // Handles a known limitation of Lucene 4.x WordDelimiterFilter where UPPER→letter
    // transitions don't trigger splits (e.g., "JSONSchema" → "JSON Schema").
    private static readonly Regex _consecutiveUppercasePattern = new(@"(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

    // Shared analyzer instance. Lucene.NET analyzers are thread-safe for GetTokenStream()
    // (the default reuse strategy uses per-thread TokenStream pooling via CloseableThreadLocal).
    private static readonly CapabilityAnalyzer _sharedAnalyzer = new();

    /// <inheritdoc />
    public HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        // Pre-process: split consecutive uppercase sequences before a new word
        // (e.g., "JSONSchema" → "JSON Schema", "MCPServer" → "MCP Server").
        text = _consecutiveUppercasePattern.Replace(text, " ");

        var tokens = new HashSet<string>(StringComparer.Ordinal);

        using var tokenStream = _sharedAnalyzer.GetTokenStream("text", text);

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
    /// Custom Lucene.NET analyzer for text matching. Applies:
    /// <list type="number">
    ///   <item>WhitespaceTokenizer — splits on whitespace, preserving case information
    ///         for downstream filters.</item>
    ///   <item>WordDelimiterFilter — splits camelCase/PascalCase identifiers
    ///         (e.g., "getRecipeSchema" → "get", "Recipe", "Schema").</item>
    ///   <item>LowerCaseFilter — normalizes to lowercase.</item>
    ///   <item>StopFilter — removes common English stop words.</item>
    ///   <item>PorterStemFilter — applies Porter stemming for morphological normalization
    ///         (e.g., "recipes" → "recip", "enabling" → "enabl").</item>
    /// </list>
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
