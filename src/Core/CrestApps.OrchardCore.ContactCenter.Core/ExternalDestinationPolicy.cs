using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Decides whether the platform is willing to place or transfer a call to an external destination.
/// <para>
/// This is a safety policy rather than a formatting concern. It is the single place that decides the answer,
/// because the question is asked at three different moments — when an administrator saves a transfer
/// destination, when a transfer is resolved at call time, and when a dial command is executed — and an
/// address that is refused at one of those moments must be refused at all of them. Keeping one definition is
/// what prevents a destination from being rejected in the settings screen but still reachable through a
/// workflow that dials it directly.
/// </para>
/// </summary>
public static class ExternalDestinationPolicy
{
    // The bound is on the whole E.164 value, so it admits seven digits after the leading plus sign. It is
    // carried over unchanged from the three implementations this replaced, because changing what is dialable
    // is not what consolidating them was for.
    private const int MinimumLength = 8;

    /// <summary>
    /// Determines whether the supplied address is an external destination the platform will dial.
    /// </summary>
    /// <param name="address">The raw address to evaluate. A value that is not already in E.164 form is refused.</param>
    /// <returns><see langword="true"/> when the address may be dialed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(string address)
    {
        if (!PhoneNumber.TryFromE164(address, out var phoneNumber))
        {
            return false;
        }

        return IsAllowed(phoneNumber);
    }

    /// <summary>
    /// Determines whether the supplied canonical number is an external destination the platform will dial.
    /// </summary>
    /// <param name="phoneNumber">The canonical number to evaluate.</param>
    /// <returns><see langword="true"/> when the number may be dialed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(PhoneNumber phoneNumber)
    {
        if (!phoneNumber.HasValue || phoneNumber.Value.Length < MinimumLength)
        {
            return false;
        }

        var digits = phoneNumber.Digits;

        return !IsEmergencyNumber(digits) && !IsPremiumNumber(digits);
    }

    /// <summary>
    /// Determines whether the supplied digits address an emergency service.
    /// </summary>
    /// <param name="digits">The digits of the address, without the leading plus sign.</param>
    /// <returns><see langword="true"/> when the digits address an emergency service; otherwise, <see langword="false"/>.</returns>
    public static bool IsEmergencyNumber(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        return digits.EndsWith("911", StringComparison.Ordinal) ||
            digits.EndsWith("112", StringComparison.Ordinal) ||
            digits.EndsWith("999", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether the supplied digits address a premium-rate service.
    /// </summary>
    /// <param name="digits">The digits of the address, without the leading plus sign.</param>
    /// <returns><see langword="true"/> when the digits address a premium-rate service; otherwise, <see langword="false"/>.</returns>
    public static bool IsPremiumNumber(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        return digits.StartsWith("1900", StringComparison.Ordinal) ||
            digits.StartsWith("1976", StringComparison.Ordinal) ||
            digits.StartsWith("4470", StringComparison.Ordinal);
    }
}
