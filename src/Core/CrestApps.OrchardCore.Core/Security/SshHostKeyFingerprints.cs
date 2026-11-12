namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// The one SSH host-key fingerprint check: a connection trusts the server only when the server's SHA-256 host-key
/// fingerprint is the one configured for it, and an empty configuration trusts no server (fail closed). Without it,
/// anyone on the network path, or a DNS change, can stand in for the server and collect the credentials and the files.
/// Every SFTP client this solution creates checks its server with <see cref="Matches"/> in its <c>HostKeyReceived</c>
/// handler.
/// </summary>
public static class SshHostKeyFingerprints
{
    /// <summary>
    /// Brings a fingerprint to one form: the base64 SHA-256 digest without the <c>SHA256:</c> prefix, padding or spaces,
    /// as <c>ssh-keygen -lf</c> prints it and as SSH.NET reports it.
    /// </summary>
    /// <param name="fingerprint">The fingerprint.</param>
    /// <returns>The normalized fingerprint, or <see langword="null"/> when it is empty.</returns>
    public static string Normalize(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        var value = fingerprint.Trim();

        if (value.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            value = value[7..];
        }

        return value.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=');
    }

    /// <summary>
    /// Gets whether a value is a SHA-256 host-key fingerprint: 32 bytes in base64, with or without the <c>SHA256:</c>
    /// prefix and padding. Use it to validate an editor's field.
    /// </summary>
    /// <param name="fingerprint">The value.</param>
    public static bool IsValid(string fingerprint)
    {
        var value = Normalize(fingerprint);

        if (value is null)
        {
            return false;
        }

        var padded = value.PadRight(value.Length + ((4 - (value.Length % 4)) % 4), '=');
        var bytes = new byte[padded.Length];

        return Convert.TryFromBase64String(padded, bytes, out var written) && written == 32;
    }

    /// <summary>
    /// Gets whether a server's fingerprint is the expected one. An empty expectation matches nothing.
    /// </summary>
    /// <param name="expected">The configured fingerprint.</param>
    /// <param name="actual">The server's SHA-256 fingerprint.</param>
    public static bool Matches(string expected, string actual)
    {
        var normalizedExpected = Normalize(expected);
        var normalizedActual = Normalize(actual);

        return normalizedExpected is not null &&
            normalizedActual is not null &&
            string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal);
    }
}
