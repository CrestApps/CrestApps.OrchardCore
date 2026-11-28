using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Core.Access;
using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Makes the token of a personal invitation link. The token is an HMAC of the invitation id keyed by the survey's
/// secret, so it can be made again for a reminder while only its hash is stored, and nobody without the secret can
/// make one.
/// </summary>
public static class SurveyInvitationTokens
{
    /// <summary>
    /// Makes the token of an invitation.
    /// </summary>
    /// <param name="secret">The survey's anonymity secret in plain text, from <see cref="SurveyAnonymitySecrets"/>.</param>
    /// <param name="invitationId">The id of the invitation.</param>
    /// <returns>The URL-safe token.</returns>
    public static string Create(string secret, string invitationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentException.ThrowIfNullOrEmpty(invitationId);

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("invitation:" + secret), Encoding.UTF8.GetBytes(invitationId));

        return WebEncoders.Base64UrlEncode(mac);
    }

    /// <summary>
    /// Gets the hash stored for a token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The lowercase SHA-256 hex.</returns>
    public static string Hash(string token)
        => SurveyRespondentKeys.Hash(token?.Trim());
}
