using CrestApps.OrchardCore.Disbursements.Models;

namespace CrestApps.OrchardCore.Disbursements.Services;

/// <summary>
/// Is told when a disbursement is recorded, batched, sent, settled, returned, failed, cancelled or reversed. The module
/// that asked for the payment reacts here, such as Payroll showing a returned deposit on the pay run. Register one with
/// <see cref="DisbursementsServiceCollectionExtensions.AddDisbursementObserver{TObserver}"/>.
/// </summary>
public interface IDisbursementObserver
{
    /// <summary>
    /// Called after a disbursement changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default);
}
