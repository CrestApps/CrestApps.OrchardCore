using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Lets go of a kept Plaid item when its bank account no longer needs it: the account was deleted or closed, or someone
/// typed other numbers over the linked ones.
/// </summary>
public sealed class PlaidBankAccountObserver : IBankAccountObserver
{
    private readonly PlaidItemCleaner _cleaner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidBankAccountObserver"/> class.
    /// </summary>
    /// <param name="cleaner">The item cleaner.</param>
    public PlaidBankAccountObserver(PlaidItemCleaner cleaner)
    {
        _cleaner = cleaner;
    }

    /// <inheritdoc />
    public async Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var account = change.Account;

        if (account is null || string.IsNullOrEmpty(account.ItemId))
        {
            return;
        }

        var released = change.Has(BankAccountChangeKind.Deleted)
            || account.Status == BankAccountStatus.Closed
            // A new link writes its numbers with Plaid as the method and replaces its own item when it finishes.
            || change.Has(BankAccountChangeKind.NumberChanged) && !string.Equals(account.VerificationMethod, PlaidConstants.ProviderName, StringComparison.Ordinal);

        if (released)
        {
            await _cleaner.RemoveForBankAccountAsync(account.ItemId, cancellationToken: cancellationToken);
        }
    }
}
