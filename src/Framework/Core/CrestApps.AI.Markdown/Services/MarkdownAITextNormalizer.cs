using CrestApps.AI.Services;

namespace CrestApps.AI.Markdown.Services;

/// <summary>
/// Markdown-aware implementation of <see cref="IAITextNormalizer"/>.
/// </summary>
public sealed class MarkdownAITextNormalizer : IAITextNormalizer
{
    public Task<string> NormalizeContentAsync(string text, CancellationToken cancellationToken = default)
        => RagTextNormalizer.NormalizeContentAsync(text, cancellationToken);

    public Task<List<string>> NormalizeAndChunkAsync(string text, CancellationToken cancellationToken = default)
        => RagTextNormalizer.NormalizeAndChunkAsync(text, cancellationToken);

    public string NormalizeTitle(string title)
        => RagTextNormalizer.NormalizeTitle(title);
}
