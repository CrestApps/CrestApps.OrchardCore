namespace CrestApps.OrchardCore.Core.Security;

/// <summary>
/// Says whether a stored secret, such as an API key or a client secret, would now be sent somewhere other than the place
/// it was given for. An editor never shows a stored secret and keeps it when the field is left blank, so without this
/// rule a user who may edit a connection, but never saw its key, could point the connection at a host of their own and
/// receive the key with the next call. An editor or recipe that changes the address or the way of signing in therefore
/// drops the stored secrets, and they must be entered again.
/// </summary>
public static class SecretDestination
{
    /// <summary>
    /// Returns whether two addresses send to the same place: the same scheme, host and port. A path change keeps the
    /// secret. A blank address is a place of its own, the provider's default one, so going from blank to an address or
    /// back is a change; an address that does not parse is never the same place as another.
    /// </summary>
    /// <param name="stored">The address the secret was stored for.</param>
    /// <param name="posted">The address the connection will use.</param>
    public static bool SameOrigin(string stored, string posted)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return string.IsNullOrWhiteSpace(posted);
        }

        return Uri.TryCreate(stored.Trim(), UriKind.Absolute, out var before) &&
            Uri.TryCreate(posted?.Trim(), UriKind.Absolute, out var after) &&
            SameOrigin(before, after);
    }

    /// <inheritdoc cref="SameOrigin(string, string)"/>
    public static bool SameOrigin(Uri stored, Uri posted)
    {
        if (stored is null)
        {
            return posted is null;
        }

        return posted is not null &&
            stored.IsAbsoluteUri &&
            posted.IsAbsoluteUri &&
            Uri.Compare(stored, posted, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>
    /// Returns whether two host names and ports name the same server, for a connection addressed by host rather than by
    /// URL, such as FTP or SFTP: the same host name, ignoring case and surrounding spaces, and the same port. A blank port
    /// is the protocol's default, a place of its own, as a blank address is.
    /// </summary>
    /// <param name="storedHost">The host the secret was stored for.</param>
    /// <param name="storedPort">The port the secret was stored for.</param>
    /// <param name="postedHost">The host the connection will use.</param>
    /// <param name="postedPort">The port the connection will use.</param>
    public static bool SameServer(string storedHost, int? storedPort, string postedHost, int? postedPort)
        => SameValue(storedHost, postedHost) && storedPort == postedPort;

    /// <summary>
    /// Returns whether a setting that decides where or how a secret is used, such as a user name, an authentication type
    /// or a host key fingerprint, is unchanged: compared without regard to case or surrounding spaces, a blank value being
    /// the same as another blank one.
    /// </summary>
    /// <param name="stored">The value the secret was stored with.</param>
    /// <param name="posted">The value the connection will use.</param>
    public static bool SameValue(string stored, string posted)
        => string.Equals(Trimmed(stored), Trimmed(posted), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the stored secret when it may still be used, or <see langword="null"/> when the connection now sends to
    /// another place or signs in another way.
    /// </summary>
    /// <param name="stored">The stored, protected secret.</param>
    /// <param name="samePlace">Whether the address and the way of signing in are unchanged.</param>
    public static string Keep(string stored, bool samePlace)
        => samePlace ? stored : null;

    private static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
