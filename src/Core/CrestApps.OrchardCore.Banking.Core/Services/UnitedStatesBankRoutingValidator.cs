using CrestApps.OrchardCore.Banking.Services;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Checks United States bank numbers: a nine-digit ABA routing number whose last digit is the check digit
/// (3, 7, 1 weights, modulo 10), and an account number of up to 17 letters and digits, the length an ACH entry holds.
/// </summary>
public sealed class UnitedStatesBankRoutingValidator : IBankRoutingValidator
{
    private const int RoutingNumberLength = 9;
    private const int MinAccountNumberLength = 4;
    private const int MaxAccountNumberLength = 17;

    private static readonly int[] _weights = [3, 7, 1, 3, 7, 1, 3, 7, 1];

    /// <inheritdoc />
    public string Country => BankingConstants.Countries.UnitedStates;

    /// <inheritdoc />
    public bool TryNormalizeRoutingNumber(string value, out string normalized)
    {
        normalized = BankNumberFormat.Clean(value);

        if (!BankNumberFormat.HasForm(normalized, RoutingNumberLength, RoutingNumberLength, digitsOnly: true) || !HasValidCheckDigit(normalized))
        {
            normalized = null;

            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryNormalizeAccountNumber(string value, out string normalized)
    {
        normalized = BankNumberFormat.Clean(value);

        if (!BankNumberFormat.HasForm(normalized, MinAccountNumberLength, MaxAccountNumberLength, digitsOnly: false))
        {
            normalized = null;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns whether a nine-digit routing number's weighted digits add up to a multiple of ten, and it is not all zeros.
    /// </summary>
    /// <param name="digits">The nine digits.</param>
    /// <returns><see langword="true"/> when the check digit is right.</returns>
    public static bool HasValidCheckDigit(string digits)
    {
        if (digits is not { Length: RoutingNumberLength } || digits.All(digit => digit == '0'))
        {
            return false;
        }

        var sum = 0;

        for (var position = 0; position < RoutingNumberLength; position++)
        {
            sum += (digits[position] - '0') * _weights[position];
        }

        return sum % 10 == 0;
    }
}
