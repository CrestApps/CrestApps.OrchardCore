using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Orchestrates the registered AI assist providers to summarize interactions and suggest dispositions.
/// Returns the first provider result, so assistance is available only when a provider is installed.
/// </summary>
public interface IContactCenterAssistService
{
    /// <summary>
    /// Gets a value indicating whether any AI assist provider is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Suggests a disposition using the first provider that returns one.
    /// </summary>
    /// <param name="context">The assist context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The suggested disposition, or <see langword="null"/> when none is available.</returns>
    Task<DispositionSuggestion> SuggestDispositionAsync(AssistContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes an interaction using the first provider that returns a summary.
    /// </summary>
    /// <param name="context">The assist context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The summary, or <see langword="null"/> when none is available.</returns>
    Task<string> SummarizeAsync(AssistContext context, CancellationToken cancellationToken = default);
}
