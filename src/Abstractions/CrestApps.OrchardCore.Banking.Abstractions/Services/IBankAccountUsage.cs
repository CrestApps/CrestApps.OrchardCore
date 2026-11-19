using CrestApps.OrchardCore.Banking.Models;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Tells whether a module's records point to a bank account, such as checks drawn on it or payments made to it. An
/// account in use can't be deleted: it is closed instead, so the records keep what they point to. Register one with
/// <see cref="BankingServiceCollectionExtensions.AddBankAccountUsage{TUsage}"/>.
/// </summary>
public interface IBankAccountUsage
{
    /// <summary>
    /// Returns why the account is in use, such as "Checks are drawn on it.", or <see langword="null"/> when no record
    /// of the module points to it.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reason, or <see langword="null"/>.</returns>
    Task<string> GetUsageAsync(BankAccount account, CancellationToken cancellationToken = default);
}
