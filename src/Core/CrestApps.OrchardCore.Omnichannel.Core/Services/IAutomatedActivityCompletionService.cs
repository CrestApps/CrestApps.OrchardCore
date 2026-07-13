using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Finalizes AI-driven automated conversations through the source-neutral activity disposition lifecycle.
/// </summary>
public interface IAutomatedActivityCompletionService
{
    /// <summary>
    /// Stores the AI session reference, appends the generated summary to activity notes, applies the AI
    /// disposition, and runs the configured subject actions.
    /// </summary>
    /// <param name="request">The automated conversation completion request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The activity disposition result.</returns>
    Task<ActivityDispositionResult> CompleteAsync(
        AutomatedActivityCompletionRequest request,
        CancellationToken cancellationToken = default);
}
