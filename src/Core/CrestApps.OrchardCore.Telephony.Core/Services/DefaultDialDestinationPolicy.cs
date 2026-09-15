using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// The default <see cref="IDialDestinationPolicy"/>. It refuses emergency short codes and premium-rate numbers,
/// refuses anything that is not a dialable address, and otherwise allows the destination.
/// </summary>
/// <remarks>
/// An emergency code is matched as the <b>whole</b> dialed digit string, after an optional trunk prefix is
/// stripped. Matching it as a suffix, which an earlier implementation did, refuses every ordinary number that
/// happens to end in those three digits while still letting a code typed behind a trunk prefix through.
/// </remarks>
public sealed class DefaultDialDestinationPolicy : IDialDestinationPolicy
{
    // The bound is on the whole E.164 value, so it admits seven digits after the leading plus sign.
    private const int MinimumLength = 8;

    // Emergency short codes in wide use, matched as the entire dialed string. A hosted number cannot reach the
    // caller's local dispatch center and carries no verified address, so the platform never places one.
    private static readonly HashSet<string> _emergencyCodes = new(StringComparer.Ordinal)
    {
        "911", "112", "999", "000", "110", "119", "100", "102", "108", "113",
        "117", "118", "122", "133", "190", "191", "192", "193", "194", "997", "998",
    };

    private static readonly string[] _premiumPrefixes = ["1900", "1976", "4470"];

    private readonly IOptionsSnapshot<TelephonySettings> _telephonySettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDialDestinationPolicy"/> class.
    /// </summary>
    /// <param name="telephonySettings">The telephony settings carrying the tenant's short-code allow-list.</param>
    public DefaultDialDestinationPolicy(IOptionsSnapshot<TelephonySettings> telephonySettings)
    {
        _telephonySettings = telephonySettings;
    }

    /// <inheritdoc/>
    public DialDestinationDecision Evaluate(string address, DialDestinationContext context)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return DialDestinationDecision.Refuse(DialDestinationOutcome.Malformed, "A destination is required.");
        }

        var dialed = StripTrunkPrefix(address.Trim(), context?.TrunkPrefix);

        // An emergency code is refused before anything else, including the tenant allow-list: no configuration
        // may turn one into an ordinary destination. A code is dialed as the whole bare string, so it is compared
        // as such rather than against the digits of a canonicalized number.
        if (_emergencyCodes.Contains(dialed))
        {
            return DialDestinationDecision.Refuse(
                DialDestinationOutcome.Emergency,
                "Emergency services cannot be dialed from this platform. Use a telephone connected to the local network.");
        }

        if (IsAllowedShortCode(dialed))
        {
            return DialDestinationDecision.Allow();
        }

        if (!PhoneNumber.TryFromE164(dialed, out var phoneNumber) ||
            !phoneNumber.HasValue ||
            phoneNumber.Value.Length < MinimumLength)
        {
            return DialDestinationDecision.Refuse(
                DialDestinationOutcome.Malformed,
                "The destination is not a dialable number in international format, for example +15551234567.");
        }

        if (IsPremiumNumber(phoneNumber.Digits))
        {
            return DialDestinationDecision.Refuse(
                DialDestinationOutcome.Premium,
                "Premium-rate destinations cannot be dialed from this platform.");
        }

        return DialDestinationDecision.Allow();
    }

    private bool IsAllowedShortCode(string dialed)
    {
        if (string.IsNullOrEmpty(dialed))
        {
            return false;
        }

        var allowed = _telephonySettings.Value?.AllowedShortCodes;

        return allowed is not null && allowed.Contains(dialed, StringComparer.Ordinal);
    }

    private static bool IsPremiumNumber(string digits)
    {
        if (string.IsNullOrEmpty(digits))
        {
            return false;
        }

        return Array.Exists(_premiumPrefixes, prefix => digits.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string StripTrunkPrefix(string address, string trunkPrefix)
    {
        // A canonical number is never behind a trunk prefix, so it is left exactly as it arrived. Whether the
        // value is canonical is answered by the one entry point that answers that question.
        if (string.IsNullOrEmpty(trunkPrefix) ||
            PhoneNumber.IsE164(address) ||
            !address.StartsWith(trunkPrefix, StringComparison.Ordinal) ||
            address.Length == trunkPrefix.Length)
        {
            return address;
        }

        return address.Substring(trunkPrefix.Length);
    }
}
