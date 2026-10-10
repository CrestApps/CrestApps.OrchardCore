namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Recognizes the names of values that must never reach a report, such as passwords, API keys, and the access and
/// refresh tokens modules keep with a user or a content item. Data sources that expose stored JSON by its property
/// names leave out every property whose path names one of these.
/// </summary>
public static class ReportSecretNames
{
    private static readonly string[] _names =
    [
        "token",
        "secret",
        "password",
        "passcode",
        "credential",
        "apikey",
        "api_key",
        "accesskey",
        "privatekey",
        "private_key",
        "signingkey",
        "encryptionkey",
        "hash",
        "stamp",
        "salt",
        "otp",
        "recoverycode",
        "authenticator",
        "cookie",
    ];

    /// <summary>
    /// Determines whether any segment of a dotted property path names a secret.
    /// </summary>
    /// <param name="path">The property path, such as <c>Connections.Phone.AccessToken</c>.</param>
    /// <returns><see langword="true"/> when the path must be left out.</returns>
    public static bool IsSecret(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.Split('.').Any(segment => _names.Any(name => segment.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }
}
