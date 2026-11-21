using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Makes the key limits count a respondent by. For an anonymous survey the key is an HMAC of the identity with the
/// survey's secret, so participation records never name the person.
/// </summary>
public static class SurveyRespondentKeys
{
    /// <summary>
    /// Gets the respondent key, or <see langword="null"/> when the respondent cannot be recognized.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="identity">Who responds.</param>
    /// <param name="invitation">The invitation, or <see langword="null"/>.</param>
    /// <returns>The key, at most 64 characters.</returns>
    public static string For(Survey survey, SurveyRespondentIdentity identity, SurveyInvitation invitation)
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

        if (survey.Access?.Anonymity != AnonymityMode.Anonymous)
        {
            return raw.Length <= 64 ? raw : Hash(raw);
        }

        var secret = string.IsNullOrEmpty(survey.AnonymitySecret) ? survey.ItemId ?? string.Empty : survey.AnonymitySecret;
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw));

        return "a:" + Convert.ToHexString(mac).Substring(0, 62).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the SHA-256 hex of a value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The lowercase hex.</returns>
    public static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
}
