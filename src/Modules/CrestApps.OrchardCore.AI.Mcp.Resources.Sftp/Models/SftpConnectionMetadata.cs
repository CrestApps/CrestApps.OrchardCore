namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Models;

/// <summary>
/// Metadata for SFTP connection settings stored in an MCP resource.
/// </summary>
public sealed class SftpConnectionMetadata
{
    /// <summary>
    /// Gets or sets the SFTP server host.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the SFTP server port. Defaults to 22.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password for authentication (stored encrypted).
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the private key for authentication (stored encrypted).
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets the passphrase for the private key (stored encrypted).
    /// </summary>
    public string Passphrase { get; set; }
}
