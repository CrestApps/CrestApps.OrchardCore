using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Keeps a bank account checks are drawn on from being deleted, so the register keeps the account its checks name.
/// </summary>
public sealed class CheckBankAccountUsage : IBankAccountUsage
{
    private readonly ICheckRecordStore _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckBankAccountUsage"/> class.
    /// </summary>
    /// <param name="store">The check register.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckBankAccountUsage(ICheckRecordStore store, IStringLocalizer<CheckBankAccountUsage> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<string> GetUsageAsync(BankAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        return string.IsNullOrEmpty(account.ItemId) || await _store.GetHighestNumberAsync(account.ItemId, cancellationToken) is null
            ? null
            : S["Checks are drawn on it."];
    }
}
