using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides optional AI assistance for the Contact Center: interaction summarization and disposition
/// suggestions. AI modules implement this seam so assistance can be added without coupling the Contact
/// Center to a specific AI provider.
/// </summary>
public interface IContactCenterAssistProvider
{
    /// <summary>
    /// Gets the order in which this provider is consulted. Lower values run first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Suggests a disposition for the interaction, or <see langword="null"/> when the provider has no suggestion.
    /// </summary>
    /// <param name="context">The assist context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The suggested disposition, or <see langword="null"/>.</returns>
    Task<DispositionSuggestion> SuggestDispositionAsync(AssistContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes the interaction, or returns <see langword="null"/> when the provider has no summary.
    /// </summary>
    /// <param name="context">The assist context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The summary text, or <see langword="null"/>.</returns>
    Task<string> SummarizeAsync(AssistContext context, CancellationToken cancellationToken = default);
}
