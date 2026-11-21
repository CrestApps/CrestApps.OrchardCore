using CrestApps.Core.AI.Sftp;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// Checks an SFTP host key fingerprint field against what the SFTP client of CrestApps.Core trusts when it connects,
/// <see cref="SftpHostKeys.Matches"/>: one fingerprint, or several separated by commas, semicolons or whitespace, each
/// in the OpenSSH SHA-256 form (<c>SHA256:</c> and base64, the prefix and the padding optional) or the legacy MD5 form
/// (32 hexadecimal digits, the colons and an <c>MD5:</c> prefix optional). Every SFTP editor validates its field with
/// <see cref="IsValid"/>, and every SFTP client checks the server with <see cref="SftpHostKeys.Require"/>.
/// </summary>
/// <remarks>
/// The client's own check decides: a fingerprint is accepted only when, given the whole field, the client trusts the
/// server key that fingerprint names. So the editor never refuses a form the client matches, and never accepts one the
/// client would match with no server, such as a mistyped key, which would leave the connection failing on every run.
/// </remarks>
public static class SftpHostKeyFingerprints
{
    private const string Sha256Prefix = "SHA256:";
    private const string Md5Prefix = "MD5:";

    // The separators SftpHostKeys splits the field on. Splitting on a character it does not would name a key the client
    // never trusts, which the check of the whole field below refuses.
    private static readonly char[] _separators = [',', ';', ' ', '\t', '\r', '\n'];

    /// <summary>
    /// Gets whether a host key fingerprint field names at least one server key, and only keys the client can trust.
    /// </summary>
    /// <param name="fingerprints">The field: one fingerprint, or several separated by commas, semicolons or whitespace.</param>
    /// <returns><see langword="true"/> when the client trusts the server key each fingerprint names.</returns>
    public static bool IsValid(string fingerprints)
    {
        if (!SftpHostKeys.IsConfigured(fingerprints))
        {
            return false;
        }

        foreach (var entry in fingerprints.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryGetServerKey(entry, out var sha256, out var md5) || !SftpHostKeys.Matches(fingerprints, sha256, md5))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Brings a field to the form it is stored in: trimmed, or <see langword="null"/> when it is blank. The client reads
    /// every form <see cref="IsValid"/> accepts, so the fingerprints are stored as they were entered.
    /// </summary>
    /// <param name="fingerprints">The field.</param>
    /// <returns>The value to store.</returns>
    public static string Normalize(string fingerprints)
        => string.IsNullOrWhiteSpace(fingerprints) ? null : fingerprints.Trim();

    // The key, as SSH.NET reports it, that a fingerprint names: a SHA-256 digest in base64 without padding, or an MD5
    // digest. The client then decides whether it trusts that key.
    private static bool TryGetServerKey(string entry, out string sha256, out byte[] md5)
    {
        sha256 = null;
        md5 = null;

        string base64;

        if (entry.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            base64 = entry[Sha256Prefix.Length..];
        }
        else
        {
            var hex = (entry.StartsWith(Md5Prefix, StringComparison.OrdinalIgnoreCase) ? entry[Md5Prefix.Length..] : entry).Replace(":", string.Empty, StringComparison.Ordinal);

            if (hex.Length == 32 && hex.All(char.IsAsciiHexDigit))
            {
                md5 = Convert.FromHexString(hex);

                return true;
            }

            base64 = entry;
        }

        var unpadded = base64.TrimEnd('=');
        var padded = unpadded.PadRight(unpadded.Length + ((4 - (unpadded.Length % 4)) % 4), '=');
        var digest = new byte[padded.Length];

        if (!Convert.TryFromBase64String(padded, digest, out var written) || written != 32)
        {
            return false;
        }

        sha256 = Convert.ToBase64String(digest, 0, written).TrimEnd('=');

        return true;
    }
}
