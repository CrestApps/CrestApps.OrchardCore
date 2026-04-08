using System.Text.RegularExpressions;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Sanitizes text for text-to-speech (TTS) by stripping markdown formatting,
/// code blocks, emoji, and other non-speech elements.
/// </summary>
public static partial class SpeechTextSanitizer
{
    /// <summary>
    /// Removes markdown formatting, code blocks, emoji, and other non-speech
    /// elements from the specified text so it can be spoken naturally by a TTS engine.
    /// </summary>
    /// <param name="text">The text to sanitize.</param>
    /// <returns>The sanitized text suitable for speech synthesis, or the original value when blank.</returns>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        // Remove fenced code blocks (```...```).
        text = FencedCodeBlockPattern().Replace(text, " ");

        // Remove inline code (`code`).
        text = InlineCodePattern().Replace(text, " ");

        // Remove markdown images ![alt](url).
        text = MarkdownImagePattern().Replace(text, " ");

        // Convert markdown links [text](url) to just text.
        text = MarkdownLinkPattern().Replace(text, "$1");

        // Remove bold/italic markers (**, *, ___, __, _).
        text = BoldItalicMarkerPattern().Replace(text, string.Empty);

        // Remove heading markers (# through ######).
        text = HeadingMarkerPattern().Replace(text, string.Empty);

        // Remove horizontal rules (---, ***, ___).
        text = HorizontalRulePattern().Replace(text, string.Empty);

        // Remove list markers (- item, * item, + item).
        text = UnorderedListMarkerPattern().Replace(text, string.Empty);

        // Remove numbered list markers (1. item, 2. item).
        text = OrderedListMarkerPattern().Replace(text, string.Empty);

        // Remove emoji surrogate pairs (supplementary plane: 😀🎉🚀 etc.).
        text = EmojiSurrogatePairPattern().Replace(text, string.Empty);

        // Remove common BMP emoji/symbol characters.
        text = BmpEmojiSymbolPattern().Replace(text, string.Empty);

        // Collapse multiple whitespace into a single space.
        text = MultipleWhitespacePattern().Replace(text, " ");

        return text.Trim();
    }

    [GeneratedRegex(@"```[\s\S]*?```")]
    private static partial Regex FencedCodeBlockPattern();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"!\[[^\]]*\]\([^\)]*\)")]
    private static partial Regex MarkdownImagePattern();

    [GeneratedRegex(@"\[([^\]]*)\]\([^\)]*\)")]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"\*{1,3}|_{1,3}")]
    private static partial Regex BoldItalicMarkerPattern();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingMarkerPattern();

    [GeneratedRegex(@"^[-*_]{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"^\s*[-*+]\s+", RegexOptions.Multiline)]
    private static partial Regex UnorderedListMarkerPattern();

    [GeneratedRegex(@"^\s*\d+\.\s+", RegexOptions.Multiline)]
    private static partial Regex OrderedListMarkerPattern();

    [GeneratedRegex(@"[\uD800-\uDBFF][\uDC00-\uDFFF]")]
    private static partial Regex EmojiSurrogatePairPattern();

    [GeneratedRegex(@"[\u2600-\u27BF\uFE00-\uFE0F\u200D]")]
    private static partial Regex BmpEmojiSymbolPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleWhitespacePattern();
}
