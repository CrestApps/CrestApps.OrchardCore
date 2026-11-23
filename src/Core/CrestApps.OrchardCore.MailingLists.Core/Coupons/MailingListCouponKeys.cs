using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.MailingLists.Core.Coupons;

/// <summary>
/// Makes the idempotency key a welcome coupon is issued under: <c>mailing-list:{listId}:{HMAC-SHA256 of the address's
/// abuse key}</c>. It is the same for the aliases of one mailbox and survives unsubscribing and signing up again (it never
/// uses the member id), so each list gives an address one coupon, ever; the address itself is never in the key.
/// </summary>
public static class MailingListCouponKeys
{
    /// <summary>
    /// The text every key starts with.
    /// </summary>
    public const string Prefix = "mailing-list:";

    /// <summary>
    /// Returns the idempotency key of a list's welcome coupon for an address.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="email">The address, in any of its alias forms.</param>
    /// <param name="secret">The tenant's coupon key.</param>
    /// <returns>The key, or <see langword="null"/> when the address is empty.</returns>
    public static string Build(string listId, string email, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(listId);
        ArgumentNullException.ThrowIfNull(secret);

        var abuseKey = EmailAddresses.AbuseKey(email);

        if (string.IsNullOrEmpty(abuseKey))
        {
            return null;
        }

        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(abuseKey));

        return $"{Prefix}{listId}:{Convert.ToHexStringLower(hash)}";
    }
}
