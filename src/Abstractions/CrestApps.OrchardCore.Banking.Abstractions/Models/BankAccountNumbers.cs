using CrestApps.OrchardCore.SensitiveData;

namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// Reads the numbers of a bank account for display, without its full account number.
/// </summary>
public static class BankAccountNumbers
{
    /// <summary>
    /// Returns whether both the routing number and the account number are stored.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns><see langword="true"/> when both numbers are stored.</returns>
    public static bool HasNumbers(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return !string.IsNullOrEmpty(account.RoutingNumber) && account.AccountNumber is { HasValue: true };
    }

    /// <summary>
    /// Returns the account number masked for display, such as <c>••••6789</c>.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The masked number, or an empty string when none is stored.</returns>
    public static string Masked(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return SensitiveMask.Mask(account.AccountNumber);
    }
}
