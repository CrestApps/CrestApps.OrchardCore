using CrestApps.OrchardCore.Banking.Disbursements.Models;

namespace CrestApps.OrchardCore.Banking.Disbursements.Services;

/// <summary>
/// Is told when a disbursement batch is made, released, reopened, cancelled or settled, and when a returns file is
/// applied, after the change is saved. Register one with
/// <see cref="DisbursementsServiceCollectionExtensions.AddDisbursementBatchObserver{TObserver}"/>.
/// </summary>
public interface IDisbursementBatchObserver
{
    /// <summary>
    /// Called after a batch changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnChangedAsync(DisbursementBatchChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the bank's returns were applied; each returned disbursement was told apart.
    /// </summary>
    /// <param name="result">What applying them did.</param>
    /// <param name="actor">The user name of who applied them, or <see langword="null"/> for the system.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnReturnsAppliedAsync(DisbursementImportResult result, string actor, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
