using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.Configuration;

/// <summary>
/// Recognizes configuration values that are only ever appropriate outside production: the sample credentials
/// checked into this repository's development assets, and the unsubstituted placeholders that a deployment
/// template leaves behind when an operator forgets to supply the real value.
/// </summary>
/// <remarks>
/// <para>
/// This exists because a development credential that reaches production is not a configuration mistake, it is a
/// published one. Every value registered here is readable by anyone who can read the repository, so a
/// deployment still using it has no secret at all. Detection is deliberately conservative: an operator whose
/// genuine production secret happens to read like a test value must not be locked out, so recognition is by
/// exact match against a closed register rather than by heuristic scoring.
/// </para>
/// <para>
/// The credentials that appear verbatim in the repository are registered as SHA-256 digests rather than as
/// literals. Storing the plaintext would add another copy of the same sample credential to the codebase and
/// give a secret scanner a second site to report, while the digest is enough to answer the only question this
/// type is asked. The digests are pinned constants; a test recomputes them from the tracked development assets
/// and fails when a new sample credential is introduced without being registered, which is what stops the
/// register from silently falling behind the files it describes.
/// </para>
/// </remarks>
public static class KnownDevelopmentValues
{
    /// <summary>
    /// SHA-256 digests, lowercase hexadecimal, of the credential literals that appear in this repository's
    /// tracked development assets.
    /// </summary>
    /// <remarks>
    /// Kept in step with the assets by <c>KnownDevelopmentValuesTests</c>, which rescans the tracked files and
    /// fails when a digest is missing or no longer used.
    /// </remarks>
    private static readonly HashSet<string> _checkedInSecretDigests = new(StringComparer.OrdinalIgnoreCase)
    {
        // src/Startup/CrestApps.Aspire.AppHost/Coturn/turnserver.conf - development coturn shared secret.
        "83b0efbe2243a63f0570c9609c49542aaceb7f127fec1a3e302d34adf78dc111",

        // src/Startup/CrestApps.Aspire.AppHost/Asterisk/ari.conf and pjsip.conf - development ARI and SIP password.
        "e2cc321af042dea7371cc769283052aaae37076acf52348fbf9eebda50f3f2e7",

        // src/Startup/CrestApps.Aspire.AppHost/Coturn/turnserver-webrtc.conf.template - unsubstituted placeholder.
        "70ab49ae273bf65681e625c124210442d12c92ff810d262dd0e480020ae25a49",
    };

    /// <summary>
    /// Values that are universally understood to mean "not configured yet". They are matched case-insensitively
    /// and are never legitimate production credentials.
    /// </summary>
    private static readonly HashSet<string> _placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "change-me",
        "change_me",
        "changeme",
        "default",
        "development",
        "dummy",
        "example",
        "fake",
        "insecure",
        "none",
        "notasecret",
        "not-a-secret",
        "password",
        "placeholder",
        "sample",
        "secret",
        "test",
        "testing",
        "todo",
        "unset",
    };

    /// <summary>
    /// Fragments that only appear in a template that was never filled in. Unlike the placeholder list these are
    /// matched as substrings, because a template usually surrounds them with instructions.
    /// </summary>
    private static readonly string[] _placeholderFragments =
    [
        "replace-with",
        "replace_with",
        "replacewith",
        "change-me",
        "changeme",
        "your-secret",
        "your_secret",
        "yoursecret",
        "todo:",
    ];

    /// <summary>
    /// Determines whether a configuration value is a known development credential or an unsubstituted
    /// placeholder.
    /// </summary>
    /// <param name="value">The configured value to inspect.</param>
    /// <returns><see langword="true"/> when the value must never be used in production; otherwise <see langword="false"/>.</returns>
    public static bool IsDevelopmentValue(string value)
        => IsDevelopmentValue(value, out _);

    /// <summary>
    /// Determines whether a configuration value is a known development credential or an unsubstituted
    /// placeholder, and describes why.
    /// </summary>
    /// <param name="value">The configured value to inspect.</param>
    /// <param name="reason">
    /// When the method returns <see langword="true"/>, a description of why the value was rejected that names
    /// no part of the value itself; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the value must never be used in production; otherwise <see langword="false"/>.</returns>
    public static bool IsDevelopmentValue(string value, out string reason)
    {
        reason = null;

        // An absent value is a different failure with a different remedy, and reporting it here would tell an
        // operator to replace a development secret they never configured.
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();

        if (_placeholders.Contains(candidate))
        {
            reason = "the value is a placeholder that means the setting was never configured";

            return true;
        }

        if (IsBracketedPlaceholder(candidate))
        {
            reason = "the value is an unsubstituted template placeholder";

            return true;
        }

        foreach (var fragment in _placeholderFragments)
        {
            if (candidate.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                reason = "the value still contains template instructions rather than a configured value";

                return true;
            }
        }

        if (_checkedInSecretDigests.Contains(ComputeDigest(candidate)))
        {
            reason = "the value is a development credential published in this repository, so it is not secret";

            return true;
        }

        return false;
    }

    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 digest used to register a checked-in development credential.
    /// </summary>
    /// <param name="value">The value to digest.</param>
    /// <returns>The lowercase hexadecimal digest.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string ComputeDigest(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(digest).ToLower(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the registered digests of the development credentials that appear in this repository.
    /// </summary>
    /// <returns>The lowercase hexadecimal digests.</returns>
    public static IReadOnlyCollection<string> GetCheckedInSecretDigests()
        => _checkedInSecretDigests;

    private static bool IsBracketedPlaceholder(string candidate)
    {
        if (candidate.Length < 3)
        {
            return false;
        }

        var first = candidate[0];
        var last = candidate[candidate.Length - 1];

        if (first == '<' && last == '>')
        {
            return true;
        }

        if (first == '[' && last == ']')
        {
            return true;
        }

        if (first == '{' && last == '}')
        {
            return true;
        }

        return candidate.StartsWith("__", StringComparison.Ordinal)
            && candidate.EndsWith("__", StringComparison.Ordinal);
    }
}
