using System.Globalization;
using CrestApps.OrchardCore.Core.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Creates and reads the signed tokens of confirmation and claim links. A token holds the member id, the member's
/// verification nonce and the time the link stops working, signed for <see cref="MailingListsConstants.VerifyTokenPurpose"/>.
/// The signature lasts <see cref="Grace"/> longer than the link itself, so a link that just expired still names its
/// member and the page can offer to send a new one; the link only verifies before its own end.
/// </summary>
public static class MailingListVerificationTokens
{
    /// <summary>
    /// How long after a link stops working its token can still be read, to offer a new link.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(30);

    private const char Separator = ':';

    /// <summary>
    /// Returns the token of a new link.
    /// </summary>
    /// <param name="tokens">The signed token service.</param>
    /// <param name="memberId">The member's id.</param>
    /// <param name="nonce">The member's current verification nonce.</param>
    /// <param name="nowUtc">The current time (UTC).</param>
    /// <param name="lifetime">How long the link works.</param>
    /// <returns>The token.</returns>
    public static string Create(SignedTokenService tokens, string memberId, string nonce, DateTime nowUtc, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentException.ThrowIfNullOrEmpty(memberId);
        ArgumentException.ThrowIfNullOrEmpty(nonce);

        return tokens.Protect(MailingListsConstants.VerifyTokenPurpose, Format(memberId, nonce, nowUtc + lifetime), lifetime + Grace);
    }

    /// <summary>
    /// Reads a token.
    /// </summary>
    /// <param name="tokens">The signed token service.</param>
    /// <param name="token">The token from the link.</param>
    /// <returns>What the token holds, or <see langword="null"/> when it was not made by this site for verifying, was
    /// altered, or is past its grace period.</returns>
    public static MailingListVerificationTicket Read(SignedTokenService tokens, string token)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        return string.IsNullOrWhiteSpace(token) ? null : Parse(tokens.Unprotect(MailingListsConstants.VerifyTokenPurpose, token));
    }

    /// <summary>
    /// Writes the value a token holds.
    /// </summary>
    /// <param name="memberId">The member's id.</param>
    /// <param name="nonce">The nonce.</param>
    /// <param name="expiresUtc">When the link stops working (UTC).</param>
    /// <returns>The value.</returns>
    public static string Format(string memberId, string nonce, DateTime expiresUtc)
        => string.Join(Separator, memberId, nonce, expiresUtc.Ticks.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Reads the value a token holds.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>What it holds, or <see langword="null"/> when it is not a verification value.</returns>
    public static MailingListVerificationTicket Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var parts = value.Split(Separator);

        if (parts.Length != 3
            || string.IsNullOrEmpty(parts[0])
            || string.IsNullOrEmpty(parts[1])
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTime.MinValue.Ticks
            || ticks > DateTime.MaxValue.Ticks)
        {
            return null;
        }

        return new MailingListVerificationTicket(parts[0], parts[1], new DateTime(ticks, DateTimeKind.Utc));
    }
}
