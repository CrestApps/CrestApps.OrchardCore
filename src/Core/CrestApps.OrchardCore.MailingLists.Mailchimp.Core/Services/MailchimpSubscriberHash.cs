using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// The id Mailchimp knows an audience member by: the MD5 hash, in lowercase hex, of the lowercase address.
/// </summary>
public static class MailchimpSubscriberHash
{
    /// <summary>
    /// Returns the subscriber hash of an address.
    /// </summary>
    /// <param name="email">The address, in any case and with any surrounding spaces.</param>
    /// <returns>The 32-character hash.</returns>
    public static string Compute(string email)
    {
        var normalized = EmailAddresses.Normalize(email);

        ArgumentException.ThrowIfNullOrEmpty(normalized, nameof(email));

        // Mailchimp names a member by this hash; it protects nothing this site keeps.
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Returns whether a value has the shape of a subscriber hash (32 lowercase hex characters).
    /// </summary>
    /// <param name="value">The value.</param>
    public static bool IsHash(string value)
    {
        if (value is null || value.Length != 32)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }
}
