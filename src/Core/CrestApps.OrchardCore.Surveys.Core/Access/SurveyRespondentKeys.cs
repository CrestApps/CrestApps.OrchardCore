using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Makes the key limits count a respondent by. For an anonymous survey the key is an HMAC of the identity with the
/// survey's secret, so participation records never name the person; the secret comes from
/// <see cref="SurveyAnonymitySecrets"/>, which keeps it out of the readable database.
/// </summary>
public static class SurveyRespondentKeys
{
    /// <summary>
    /// The longest key; a longer raw key is hashed.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Gets the respondent key, or <see langword="null"/> when the respondent cannot be recognized.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="secret">The survey's anonymity secret in plain text; required for an anonymous survey.</param>
    /// <param name="identity">Who responds.</param>
    /// <param name="invitation">The invitation, or <see langword="null"/>.</param>
    /// <returns>The key, at most 64 characters.</returns>
    public static string For(Survey survey, string secret, SurveyRespondentIdentity identity, SurveyInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var raw = !string.IsNullOrEmpty(identity?.UserId)
            ? "u:" + identity.UserId
            : invitation is not null
                ? "i:" + invitation.ItemId
                : !string.IsNullOrEmpty(identity?.BrowserKey) ? "b:" + identity.BrowserKey : null;

        if (raw is null)
        {
            return null;
        }

        if (!SurveyAnonymity.IsAnonymous(survey))
        {
            return Plain(raw);
        }

        return Anonymous(secret, raw);
    }

    /// <summary>
    /// Gets every key a user's participation may be stored under: the plain key of identified surveys and the
    /// pseudonymous key of each anonymous survey whose secret is given.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="anonymousSecrets">The secrets of the anonymous surveys, in plain text.</param>
    /// <returns>The keys.</returns>
    public static IReadOnlyCollection<string> ForUser(string userId, IEnumerable<string> anonymousSecrets)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var raw = "u:" + userId;
        var keys = new HashSet<string>(StringComparer.Ordinal) { Plain(raw) };

        foreach (var secret in anonymousSecrets ?? [])
        {
            if (!string.IsNullOrEmpty(secret))
            {
                keys.Add(Anonymous(secret, raw));
            }
        }

        return keys;
    }

    /// <summary>
    /// Gets the seal an anonymous response keeps of the personal invitation it was started from, so the invitation can be
    /// marked completed without the response naming it: an HMAC of the invitation id with the survey's secret, which
    /// nobody without the secret can link back to the invitation.
    /// </summary>
    /// <param name="secret">The survey's anonymity secret in plain text.</param>
    /// <param name="invitationId">The id of the invitation.</param>
    /// <returns>The seal.</returns>
    public static string InvitationSeal(string secret, string invitationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentException.ThrowIfNullOrEmpty(invitationId);

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("seal:" + secret), Encoding.UTF8.GetBytes(invitationId));

        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the lookup value an invitation keeps in its index so the invitation of an anonymous response's seal is found
    /// with one indexed query: a second HMAC, of the seal, keyed by the survey's secret. Someone who reads the database
    /// sees both the seal and the lookup value, but can't turn one into the other without the secret, so the response
    /// still can't be linked to its invitation without it.
    /// </summary>
    /// <param name="secret">The survey's anonymity secret in plain text.</param>
    /// <param name="seal">The seal, from <see cref="InvitationSeal"/>.</param>
    /// <returns>The lookup value.</returns>
    public static string InvitationSealLookup(string secret, string seal)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentException.ThrowIfNullOrEmpty(seal);

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("seal-lookup:" + secret), Encoding.UTF8.GetBytes(seal));

        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the SHA-256 hex of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The lowercase hex.</returns>
    public static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

    private static string Plain(string raw)
        => raw.Length <= MaxLength ? raw : Hash(raw);

    private static string Anonymous(string secret, string raw)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("An anonymous survey needs its anonymity secret to make a respondent key.");
        }

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw));

        return "a:" + Convert.ToHexString(mac).Substring(0, 62).ToLowerInvariant();
    }
}
