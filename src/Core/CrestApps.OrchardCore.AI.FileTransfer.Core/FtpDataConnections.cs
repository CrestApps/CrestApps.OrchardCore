using FluentFTP;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core;

/// <summary>
/// The FTP data connection types that keep the data channel on the server the connection names. In passive mode the
/// server's answer names the address the client connects to for each file; every mode but extended passive (EPSV, which
/// sends only a port) and PASVEX (which ignores the address the server sends) connects wherever it says, so a server could
/// send the data channel to the site's own network. An empty value is FluentFTP's AutoPassive, which also uses the
/// address of a plain PASV answer. Values are read the way the FTP client reads them (FluentFTP's
/// <see cref="FtpDataConnectionType"/>, ignoring case, numbers and combined flags included), so a value written another
/// way can't slip past.
/// </summary>
public static class FtpDataConnections
{
    /// <summary>
    /// The data connection type a connection is held to when the site's own network isn't allowed and its own isn't safe.
    /// </summary>
    public const string Safe = nameof(FtpDataConnectionType.PASVEX);

    /// <summary>
    /// Whether the data connection type keeps the data channel on the server the connection names.
    /// </summary>
    /// <param name="value">The stored value, or <see langword="null"/> for the client's default.</param>
    public static bool IsSafe(string value)
        => !string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<FtpDataConnectionType>(value, ignoreCase: true, out var type) &&
            type is FtpDataConnectionType.EPSV or FtpDataConnectionType.PASVEX;

    /// <summary>
    /// The value a connection uses: its own when that is safe or the site's own network is allowed, otherwise
    /// <see cref="Safe"/>.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <param name="allowPrivateNetwork">Whether the host allows file transfers to reach the site's own network.</param>
    public static string Effective(string value, bool allowPrivateNetwork)
        => allowPrivateNetwork || IsSafe(value) ? value : Safe;
}
