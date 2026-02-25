using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Provides text normalization and chunking utilities for RAG (Retrieval-Augmented Generation) content.
/// Strips HTML tags, uses <see cref="MarkdownReader"/> for Markdown-to-plain-text conversion,
/// and provides token-aware chunking via <see cref="DocumentTokenChunker"/>.
/// </summary>
public static partial class RagTextNormalizer
{
    private static readonly MarkdownReader _reader = new();

    private static readonly DocumentTokenChunker _defaultChunker = new(
        new IngestionChunkerOptions(TiktokenTokenizer.CreateForModel("gpt-4o"))
        {
            MaxTokensPerChunk = 500,
            OverlapTokens = 50,
        });

    /// <summary>
    /// Normalizes content text for RAG by stripping HTML, parsing Markdown via
    /// <see cref="MarkdownReader"/>, and normalizing whitespace. Preserves meaningful
    /// paragraph breaks while removing all formatting artifacts.
    /// </summary>
    /// <param name="text">The raw text that may contain HTML, Markdown, or escaped HTML.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Clean plain text suitable for chunking and embedding, or the original value if null/empty.</returns>
    public static async Task<string> NormalizeContentAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var document = await ParseDocumentAsync(StripHtml(text), cancellationToken);
        var normalized = JoinDocumentText(document);

        return NormalizeContentWhitespace(normalized).Trim();
    }

    /// <summary>
    /// Normalizes content text and splits it into embedding-ready chunks using
    /// <see cref="DocumentTokenChunker"/> with token-aware boundaries.
    /// </summary>
    /// <param name="text">The raw text that may contain HTML, Markdown, or escaped HTML.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of normalized text chunks suitable for embedding, or an empty list if the input is null/empty.</returns>
    public static async Task<List<string>> NormalizeAndChunkAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var document = await ParseDocumentAsync(StripHtml(text), cancellationToken);
        var chunks = new List<string>();

        await foreach (var chunk in _defaultChunker.ProcessAsync(document, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(chunk.Content))
            {
                chunks.Add(chunk.Content);
            }
        }

        return chunks;
    }

    /// <summary>
    /// Normalizes a title by stripping HTML and collapsing to a single line.
    /// This is a synchronous operation suitable for short text like titles.
    /// </summary>
    /// <param name="title">The raw title that may contain HTML or Markdown.</param>
    /// <returns>Clean single-line plain text title, or the original value if null/empty.</returns>
    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        title = StripHtml(title);
        title = AllWhitespaceRegex().Replace(title, " ");

        return title.Trim();
    }

    internal static string StripHtml(string text)
    {
        // Convert <br> variants to newlines.
        text = BrTagRegex().Replace(text, "\n");

        // Add newlines for block-level closing tags.
        text = BlockCloseTagRegex().Replace(text, "\n");

        // Strip all remaining HTML tags.
        text = HtmlTagRegex().Replace(text, string.Empty);

        // Decode HTML entities (e.g., &amp; → &, &#x00B6; → ¶).
        text = WebUtility.HtmlDecode(text);

        // Remove pilcrow signs (¶) commonly left over from Markdown headerlink anchors.
        text = text.Replace("\u00B6", string.Empty);

        return text;
    }

    private static async Task<IngestionDocument> ParseDocumentAsync(string text, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        return await _reader.ReadAsync(stream, "inmemory", "text/markdown", cancellationToken);
    }

    private static string JoinDocumentText(IngestionDocument document)
    {
        return string.Join("\n", document.EnumerateContent()
            .Select(e => e.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    private static string NormalizeContentWhitespace(string text)
    {
        // Collapse runs of horizontal whitespace (spaces, tabs) to single spaces.
        text = HorizontalSpacesRegex().Replace(text, " ");

        // Collapse 3+ consecutive newlines to double newline (preserve paragraph breaks).
        text = MultipleNewlinesRegex().Replace(text, "\n\n");

        return text;
    }

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"</(p|div|h[1-6]|li|tr|blockquote|pre|section|article|header|footer|nav|main|aside)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockCloseTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalSpacesRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex AllWhitespaceRegex();
}
