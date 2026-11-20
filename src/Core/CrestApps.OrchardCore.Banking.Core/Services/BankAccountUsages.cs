using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Asks every <see cref="IBankAccountUsage"/> whether records point to a bank account.
/// </summary>
public static class BankAccountUsages
{
    /// <summary>
    /// Returns why records point to an account.
    /// </summary>
    /// <param name="usages">The modules whose records point to accounts.</param>
    /// <param name="account">The account.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The reasons; empty when nothing points to the account.</returns>
    public static async Task<IReadOnlyList<string>> GetReasonsAsync(IEnumerable<IBankAccountUsage> usages, BankAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usages);
        ArgumentNullException.ThrowIfNull(account);

        var reasons = new List<string>();

        foreach (var usage in usages)
        {
            if (await usage.GetUsageAsync(account, cancellationToken) is { Length: > 0 } reason)
            {
                reasons.Add(reason);
            }
        }

        return reasons;
    }
}
