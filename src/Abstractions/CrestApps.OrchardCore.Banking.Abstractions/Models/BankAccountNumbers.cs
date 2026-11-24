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
    /// Returns the account number masked for display, such as <c>••••6789</c>: the stored number's hint, or the
    /// <see cref="BankAccount.AccountNumberLastFour"/> of an account whose full number the site doesn't hold.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The masked number, or an empty string when neither is stored.</returns>
    public static string Masked(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return account.AccountNumber is { HasValue: true }
            ? SensitiveMask.Mask(account.AccountNumber)
            : SensitiveMask.MaskHint(account.AccountNumberLastFour);
    }

    /// <summary>
    /// Returns the last digits shown for an account: the stored number's hint, or its
    /// <see cref="BankAccount.AccountNumberLastFour"/>.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The last digits, or <see langword="null"/> when neither is stored.</returns>
    public static string LastDigits(BankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.AccountNumber is { HasValue: true })
        {
            return account.AccountNumber.Hint;
        }

        return string.IsNullOrEmpty(account.AccountNumberLastFour) ? null : account.AccountNumberLastFour;
    }

    /// <summary>
    /// Returns whether a value can be stored as <see cref="BankAccount.AccountNumberLastFour"/>: empty, or one to four
    /// letters or digits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value is empty or has that form.</returns>
    public static bool IsValidLastFour(string value)
        => string.IsNullOrEmpty(value) || (value.Length <= 4 && value.All(char.IsAsciiLetterOrDigit));
}
