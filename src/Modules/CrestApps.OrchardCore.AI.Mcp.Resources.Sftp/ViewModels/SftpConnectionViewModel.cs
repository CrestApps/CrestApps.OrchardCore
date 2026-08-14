namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.ViewModels;

/// <summary>
/// Represents the view model for sftp connection.
/// </summary>
public class SftpConnectionViewModel
{
    /// <summary>
    /// Gets or sets the host.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has password.
    /// </summary>
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets the private key.
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has private key.
    /// </summary>
    public bool HasPrivateKey { get; set; }

    /// <summary>
    /// Gets or sets the passphrase.
    /// </summary>
    public string Passphrase { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has passphrase.
    /// </summary>
    public bool HasPassphrase { get; set; }

    /// <summary>
    /// Gets or sets the proxy type.
    /// </summary>
    public string ProxyType { get; set; }

    /// <summary>
    /// Gets or sets the proxy host.
    /// </summary>
    public string ProxyHost { get; set; }

    /// <summary>
    /// Gets or sets the proxy port.
    /// </summary>
    public int? ProxyPort { get; set; }

    /// <summary>
    /// Gets or sets the proxy username.
    /// </summary>
    public string ProxyUsername { get; set; }

    /// <summary>
    /// Gets or sets the proxy password.
    /// </summary>
    public string ProxyPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has proxy password.
    /// </summary>
    public bool HasProxyPassword { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout.
    /// </summary>
    public int? ConnectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the keep alive interval.
    /// </summary>
    public int? KeepAliveInterval { get; set; }
}
