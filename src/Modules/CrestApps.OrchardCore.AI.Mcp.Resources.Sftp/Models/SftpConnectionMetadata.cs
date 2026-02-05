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

    /// <summary>
    /// Gets or sets the proxy type.
    /// Valid values: "None", "Socks4", "Socks5", "Http".
    /// </summary>
    public string ProxyType { get; set; }

    /// <summary>
    /// Gets or sets the proxy server host.
    /// </summary>
    public string ProxyHost { get; set; }

    /// <summary>
    /// Gets or sets the proxy server port.
    /// </summary>
    public int? ProxyPort { get; set; }

    /// <summary>
    /// Gets or sets the proxy authentication username.
    /// </summary>
    public string ProxyUsername { get; set; }

    /// <summary>
    /// Gets or sets the proxy authentication password (stored encrypted).
    /// </summary>
    public string ProxyPassword { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int? ConnectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive interval in seconds.
    /// When set, periodic keep-alive messages are sent to prevent the connection from being dropped.
    /// </summary>
    public int? KeepAliveInterval { get; set; }
}
