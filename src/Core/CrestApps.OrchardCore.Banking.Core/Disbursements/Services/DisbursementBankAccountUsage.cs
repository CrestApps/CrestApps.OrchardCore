using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core.Services;

/// <summary>
/// Keeps a bank account money was sent from or to from being deleted, so each disbursement keeps the accounts it names.
/// </summary>
public sealed class DisbursementBankAccountUsage : IBankAccountUsage
{
    private readonly IDisbursementStore _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementBankAccountUsage"/> class.
    /// </summary>
    /// <param name="store">The disbursements.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementBankAccountUsage(IDisbursementStore store, IStringLocalizer<DisbursementBankAccountUsage> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<string> GetUsageAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        return string.IsNullOrEmpty(account.ItemId) || !await _store.UsesBankAccountAsync(account.ItemId, cancellationToken)
            ? null
            : S["Money was sent from or to it."];
    }
}
