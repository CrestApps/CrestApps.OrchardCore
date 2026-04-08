namespace CrestApps.Core.AI.Services;

/// <summary>
/// Normalizes titles and content used by RAG pipelines, and chunks normalized
/// content for embedding/indexing when needed.
/// </summary>
public interface IAITextNormalizer
{
    Task<string> NormalizeContentAsync(string text, CancellationToken cancellationToken = default);

    Task<List<string>> NormalizeAndChunkAsync(string text, CancellationToken cancellationToken = default);

    string NormalizeTitle(string title);
}
