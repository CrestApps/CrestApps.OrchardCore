using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Is told when a check is issued, printed, voided, stopped, cleared, reissued or goes stale, after the change is
/// saved in the unit of work. The module that issued the check reacts here, such as Payroll marking a pay statement
/// unpaid when its check is voided. Register one with
/// <see cref="ChecksServiceCollectionExtensions.AddCheckObserver{TObserver}"/>.
/// </summary>
public interface ICheckObserver
{
    /// <summary>
    /// Called after a check changed.
    /// </summary>
    /// <param name="change">What changed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default);
}
