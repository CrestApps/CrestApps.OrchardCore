namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Writes the natural language part of an answer from its results, such as a summary (<c>summarize</c> mode) or an
/// answer to the question (<c>generate</c> mode).
/// </summary>
public interface INLWebResponseGenerator
{
    /// <summary>
    /// Checks whether the generator writes text for a response mode.
    /// </summary>
    /// <param name="mode">The response mode.</param>
    /// <returns><see langword="true"/> when the generator handles the mode.</returns>
    bool CanGenerate(string mode);

    /// <summary>
    /// Writes the text for the results of the question.
    /// </summary>
    /// <param name="context">The ask context, with its results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The text, or <see langword="null"/> when there is nothing to say.</returns>
    Task<string> GenerateAsync(NLWebAskContext context, CancellationToken cancellationToken);
}
