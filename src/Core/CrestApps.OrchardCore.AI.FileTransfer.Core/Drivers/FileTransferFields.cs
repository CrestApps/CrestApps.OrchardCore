using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;

/// <summary>
/// Small helpers the FTP and SFTP connection editors share.
/// </summary>
internal static class FileTransferFields
{
    /// <summary>
    /// Trims a posted value and stores a blank one as <see langword="null"/>.
    /// </summary>
    /// <param name="value">The posted value.</param>
    /// <returns>The value to store.</returns>
    public static string Trimmed(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Gets whether a connection still signs in to the same account on the same server: the same host, port and user
    /// name. A stored password or key was given for that account, so any other one drops it: sent to another server it
    /// would reach whoever runs that server, and a key could sign in to another server or account that trusts it.
    /// </summary>
    /// <param name="storedHost">The stored host.</param>
    /// <param name="storedPort">The stored port.</param>
    /// <param name="storedUsername">The stored user name.</param>
    /// <param name="host">The posted host.</param>
    /// <param name="port">The posted port.</param>
    /// <param name="username">The posted user name.</param>
    /// <returns><see langword="true"/> when the account is the same.</returns>
    public static bool SameAccount(string storedHost, int? storedPort, string storedUsername, string host, int? port, string username)
        => SecretDestination.SameServer(storedHost, storedPort, host, port) && SecretDestination.SameValue(storedUsername, username);
}
