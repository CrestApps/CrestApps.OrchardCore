using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

/// <summary>
/// The data's postal code patterns, compiled once each.
/// </summary>
internal static class AddressPatterns
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);
    private static readonly ConcurrentDictionary<string, Regex> _whole = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Regex> _prefixes = new(StringComparer.Ordinal);

    public static bool IsMatch(string pattern, string value)
        => Matches(_whole.GetOrAdd(pattern, static pattern => Create("^(?:" + pattern + ")$")), value);

    public static bool StartsWith(string prefixPattern, string value)
        => Matches(_prefixes.GetOrAdd(prefixPattern, static pattern => Create("^(?:" + pattern + ")")), value);

    private static Regex Create(string pattern)
        => new(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, _timeout);

    private static bool Matches(Regex regex, string value)
    {
        try
        {
            return regex.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            // A pattern that cannot decide quickly never blocks someone from entering their address.
            return true;
        }
    }
}
