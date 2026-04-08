using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DataIngestion.Chunkers;
using Microsoft.ML.Tokenizers;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Provides text normalization and chunking utilities for RAG (Retrieval-Augmented Generation) content.
/// Strips HTML tags, uses <see cref="MarkdownReader"/> for Markdown-to-plain-text conversion,
/// and provides token-aware chunking via <see cref="DocumentTokenChunker"/>.
/// </summary>
public static partial class RagTextNormalizer
{
    private static readonly MarkdownReader _reader = CreateMarkdownReader();

    private static readonly DocumentTokenChunker _defaultChunker = CreateDefaultChunker();

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
        text = BrTagRegex().Replace(text, "\n");
        text = BlockCloseTagRegex().Replace(text, "\n");
        text = HtmlTagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\u00B6", string.Empty);

        return text;
    }

    internal static MarkdownReader CreateMarkdownReader() => new();

    internal static DocumentTokenChunker CreateDefaultChunker()
        => new(
            new IngestionChunkerOptions(TiktokenTokenizer.CreateForModel("gpt-4o"))
            {
                MaxTokensPerChunk = 500,
                OverlapTokens = 50,
            });

    private static async Task<IngestionDocument> ParseDocumentAsync(string text, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        return await _reader.ReadAsync(stream, "inmemory", "text/markdown", cancellationToken);
    }

    private static string JoinDocumentText(IngestionDocument document)
        => string.Join("\n", document.EnumerateContent()
            .Select(e => e.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t)));

    private static string NormalizeContentWhitespace(string text)
    {
        text = HorizontalSpacesRegex().Replace(text, " ");
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
