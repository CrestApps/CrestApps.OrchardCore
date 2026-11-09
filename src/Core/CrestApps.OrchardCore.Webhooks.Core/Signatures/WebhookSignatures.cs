using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Webhooks.Core.Signatures;

/// <summary>
/// Shared helpers for checking webhook signatures.
/// </summary>
public static class WebhookSignatures
{
    /// <summary>
    /// Compares two signature strings in time that does not depend on where they differ, so a caller cannot learn a
    /// valid signature one character at a time. A missing value never matches.
    /// </summary>
    /// <param name="expected">The signature computed locally.</param>
    /// <param name="actual">The signature the sender sent.</param>
    /// <returns><see langword="true"/> when both are present and equal.</returns>
    public static bool FixedTimeEquals(string expected, string actual)
    {
        if (expected is null || actual is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}
