using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// Told after each change of an approval request: notices, workflows and other modules react.
/// </summary>
public interface IApprovalObserver
{
    /// <summary>
    /// Called after a change.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task OnChangedAsync(ApprovalChange change, CancellationToken cancellationToken = default);
}
