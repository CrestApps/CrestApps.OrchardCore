using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// Told when a request about a subject of its kind is decided or cancelled, inside the same unit of work, so the
/// subject moves with the decision (a pay run becomes approved). Register one per subject kind with
/// <see cref="ApprovalsServiceCollectionExtensions.AddApprovalSubjectHandler{THandler}"/>.
/// </summary>
public interface IApprovalSubjectHandler
{
    /// <summary>
    /// Gets the subject kind it handles.
    /// </summary>
    string SubjectKind { get; }

    /// <summary>
    /// Called after a request is approved, rejected or cancelled.
    /// </summary>
    /// <param name="request">The request, with its final status.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the subject is updated.</returns>
    Task ClosedAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}
