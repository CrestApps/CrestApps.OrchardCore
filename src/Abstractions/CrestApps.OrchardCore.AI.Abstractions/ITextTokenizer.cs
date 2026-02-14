namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Tokenizes text into a set of normalized terms for matching and scoring.
/// </summary>
/// <remarks>
/// Implementations should handle code identifiers (camelCase/PascalCase splitting),
/// stop word removal, stemming, and case normalization for optimal matching results.
/// </remarks>
public interface ITextTokenizer
{
    /// <summary>
    /// Tokenizes the given text into a set of distinct, normalized tokens.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <returns>A set of unique tokens, or an empty set if the input is null or whitespace.</returns>
    HashSet<string> Tokenize(string text);
}
