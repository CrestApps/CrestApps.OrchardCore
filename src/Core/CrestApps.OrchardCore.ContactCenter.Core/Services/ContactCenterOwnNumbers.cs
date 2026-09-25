using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Answers whether a number is one of the contact center's own, whichever module configured it.
/// </summary>
public static class ContactCenterOwnNumbers
{
    /// <summary>
    /// Returns whether <paramref name="number"/> is the same line as any number a source names.
    /// </summary>
    /// <param name="sources">The own-number sources registered for the tenant.</param>
    /// <param name="number">The number to check, in any format.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the number is one of the contact center's own.</returns>
    public static async Task<bool> ContainsAsync(
        IEnumerable<IContactCenterOwnNumberSource> sources,
        string number,
        CancellationToken cancellationToken = default)
    {
        var key = KeyOf(number);

        if (key.Length == 0 || sources is null)
        {
            return false;
        }

        foreach (var source in sources)
        {
            var numbers = await source.GetOwnNumbersAsync(cancellationToken);

            if (numbers is not null && numbers.Any(candidate => string.Equals(KeyOf(candidate), key, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether two numbers are the same line, treating a North American number written with and without
    /// its leading country code as one.
    /// </summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns><see langword="true"/> when both name the same line.</returns>
    public static bool IsSameLine(string left, string right)
    {
        var leftKey = KeyOf(left);

        return leftKey.Length > 0 && string.Equals(leftKey, KeyOf(right), StringComparison.Ordinal);
    }

    private static string KeyOf(string number)
    {
        var digits = PhoneNumberComparisonKey.For(default, number);

        if (digits.Length == 11 && digits[0] == '1')
        {
            return digits[1..];
        }

        return digits;
    }
}
