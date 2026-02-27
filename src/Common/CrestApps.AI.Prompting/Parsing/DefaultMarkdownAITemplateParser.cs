using System.Text.Json;
using CrestApps.AI.Prompting.Models;

namespace CrestApps.AI.Prompting.Parsing;

/// <summary>
/// Markdown parser that extracts front matter metadata from AI template files.
/// Front matter is delimited by <c>---</c> markers at the start of the file.
/// Fenced <c>```json</c> blocks are automatically compacted to reduce token usage.
/// </summary>
/// <example>
/// ---
/// Title: My Prompt
/// Description: A helpful prompt
/// IsListable: true
/// Category: General
/// CustomKey: CustomValue
/// ---
///
/// You are an AI assistant...
/// </example>
public sealed class DefaultMarkdownAITemplateParser : IAITemplateParser
{
    private const string FrontMatterDelimiter = "---";
    private const string JsonFenceOpen = "```json";
    private const string FenceClose = "```";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions { get; } = [".md"];

    public AITemplateParseResult Parse(string rawContent)
    {
        var result = new AITemplateParseResult();

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            result.Body = string.Empty;

            return result;
        }

        var content = rawContent.AsSpan();

        // Check if the content starts with front matter delimiter.
        var trimmedStart = content.TrimStart();
        if (!trimmedStart.StartsWith(FrontMatterDelimiter))
        {
            result.Body = CompactJsonBlocks(rawContent.Trim());

            return result;
        }

        // Find the opening delimiter.
        var firstDelimiterEnd = IndexOfLineEnd(content, IndexOf(content, FrontMatterDelimiter));
        if (firstDelimiterEnd < 0)
        {
            result.Body = CompactJsonBlocks(rawContent.Trim());

            return result;
        }

        // Find the closing delimiter.
        var afterFirstDelimiter = content[(firstDelimiterEnd + 1)..];
        var secondDelimiterIndex = IndexOf(afterFirstDelimiter, FrontMatterDelimiter);
        if (secondDelimiterIndex < 0)
        {
            result.Body = CompactJsonBlocks(rawContent.Trim());

            return result;
        }

        // Extract front matter block.
        var frontMatter = afterFirstDelimiter[..secondDelimiterIndex];
        ParseFrontMatter(frontMatter, result.Metadata);

        // Extract body (everything after the closing delimiter line).
        var bodyStart = firstDelimiterEnd + 1 + secondDelimiterIndex;
        var closingDelimiterEnd = IndexOfLineEnd(content, bodyStart);
        if (closingDelimiterEnd >= 0 && closingDelimiterEnd < content.Length)
        {
            result.Body = CompactJsonBlocks(content[(closingDelimiterEnd + 1)..].Trim().ToString());
        }
        else
        {
            result.Body = string.Empty;
        }

        return result;
    }

    /// <summary>
    /// Compacts JSON within fenced <c>```json</c> code blocks.
    /// Pretty-printed JSON is re-serialized without indentation to reduce token usage.
    /// The fences themselves are preserved in the output.
    /// </summary>
    internal static string CompactJsonBlocks(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        var searchStart = 0;
        var result = body;

        while (true)
        {
            var fenceStart = result.IndexOf(JsonFenceOpen, searchStart, StringComparison.OrdinalIgnoreCase);
            if (fenceStart < 0)
            {
                break;
            }

            var jsonStart = fenceStart + JsonFenceOpen.Length;

            // Move past optional whitespace/newline after ```json
            while (jsonStart < result.Length && (result[jsonStart] == '\r' || result[jsonStart] == '\n'))
            {
                jsonStart++;
            }

            var fenceEnd = result.IndexOf(FenceClose, jsonStart, StringComparison.Ordinal);
            if (fenceEnd < 0)
            {
                break;
            }

            // Extract the JSON content between fences.
            var jsonContent = result[jsonStart..fenceEnd].Trim();

            if (jsonContent.Length > 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(jsonContent);
                    var compacted = JsonSerializer.Serialize(doc, JsonCompactContext.Default.JsonDocument);

                    if (compacted.Length < jsonContent.Length)
                    {
                        // Replace the entire fenced block: ```json\n{pretty}\n``` → ```json\n{compact}\n```
                        var blockStart = fenceStart;
                        var blockEnd = fenceEnd + FenceClose.Length;
                        var replacement = JsonFenceOpen + "\n" + compacted + "\n" + FenceClose;

                        result = string.Concat(result.AsSpan(0, blockStart), replacement, result.AsSpan(blockEnd));
                        searchStart = blockStart + replacement.Length;
                        continue;
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON; leave as-is.
                }
            }

            searchStart = fenceEnd + FenceClose.Length;
        }

        return result;
    }

    private static void ParseFrontMatter(ReadOnlySpan<char> frontMatter, AITemplateMetadata metadata)
    {
        foreach (var line in EnumerateLines(frontMatter))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.IsEmpty)
            {
                continue;
            }

            var colonIndex = trimmedLine.IndexOf(':');
            if (colonIndex <= 0)
            {
                continue;
            }

            var key = trimmedLine[..colonIndex].Trim().ToString();
            var value = trimmedLine[(colonIndex + 1)..].Trim().ToString();

            SetMetadataValue(metadata, key, value);
        }
    }

    private static void SetMetadataValue(AITemplateMetadata metadata, string key, string value)
    {
        if (string.Equals(key, nameof(AITemplateMetadata.Title), StringComparison.OrdinalIgnoreCase))
        {
            metadata.Title = value;
        }
        else if (string.Equals(key, nameof(AITemplateMetadata.Description), StringComparison.OrdinalIgnoreCase))
        {
            metadata.Description = value;
        }
        else if (string.Equals(key, nameof(AITemplateMetadata.IsListable), StringComparison.OrdinalIgnoreCase))
        {
            if (bool.TryParse(value, out var isListable))
            {
                metadata.IsListable = isListable;
            }
        }
        else if (string.Equals(key, nameof(AITemplateMetadata.Category), StringComparison.OrdinalIgnoreCase))
        {
            metadata.Category = value;
        }
        else
        {
            metadata.AdditionalProperties[key] = value;
        }
    }

    private static int IndexOf(ReadOnlySpan<char> content, string value)
    {
        return content.IndexOf(value.AsSpan(), StringComparison.Ordinal);
    }

    private static int IndexOfLineEnd(ReadOnlySpan<char> content, int startIndex)
    {
        for (var i = startIndex; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                return i;
            }
        }

        return content.Length - 1;
    }

    private static LineEnumerator EnumerateLines(ReadOnlySpan<char> span)
    {
        return new LineEnumerator(span);
    }

    private ref struct LineEnumerator
    {
        private ReadOnlySpan<char> _remaining;
        private ReadOnlySpan<char> _current;
        private bool _started;

        public LineEnumerator(ReadOnlySpan<char> span)
        {
            _remaining = span;
            _current = default;
            _started = false;
        }

        public readonly LineEnumerator GetEnumerator() => this;

        public readonly ReadOnlySpan<char> Current => _current;

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
            }

            if (_remaining.IsEmpty)
            {
                return false;
            }

            var newLineIndex = _remaining.IndexOfAny('\r', '\n');
            if (newLineIndex < 0)
            {
                _current = _remaining;
                _remaining = default;

                return true;
            }

            _current = _remaining[..newLineIndex];

            if (newLineIndex + 1 < _remaining.Length &&
                _remaining[newLineIndex] == '\r' &&
                _remaining[newLineIndex + 1] == '\n')
            {
                _remaining = _remaining[(newLineIndex + 2)..];
            }
            else
            {
                _remaining = _remaining[(newLineIndex + 1)..];
            }

            return true;
        }
    }
}
