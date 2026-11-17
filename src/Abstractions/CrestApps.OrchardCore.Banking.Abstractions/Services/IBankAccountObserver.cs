using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Is told when a bank account is added, changed, removed or its full number is shown, after the change is saved in
/// the unit of work. A consumer reacts here: Payroll sends the owner a security notice when a number changes, the
/// Audit Trail records every change. Register one with
/// <see cref="BankingServiceCollectionExtensions.AddBankAccountObserver{TObserver}"/>.
/// </summary>
public interface IBankAccountObserver
{
    /// <summary>
    /// Called after a bank account changed.
    /// </summary>
    /// <param name="change">What changed. It never holds the account number in clear text.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the observer is done.</returns>
    Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default);
}
