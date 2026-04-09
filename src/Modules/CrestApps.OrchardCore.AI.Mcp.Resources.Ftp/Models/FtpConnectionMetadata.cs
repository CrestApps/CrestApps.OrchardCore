namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Models;

/// <summary>
/// Metadata for FTP connection settings stored in an MCP resource.
/// </summary>
public sealed class FtpConnectionMetadata
{
    /// <summary>
    /// Gets or sets the FTP server host.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the FTP server port. Defaults to 21 for FTP, 990 for implicit FTPS.
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
    /// Gets or sets the encryption mode.
    /// Valid values: "None", "Implicit", "Explicit", "Auto".
    /// </summary>
    public string EncryptionMode { get; set; }

    /// <summary>
    /// Gets or sets the data connection type.
    /// Valid values: "AutoPassive", "PASV", "PASVEX", "EPSV", "AutoActive", "PORT", "EPRT".
    /// </summary>
    public string DataConnectionType { get; set; }

    /// <summary>
    /// Gets or sets whether to accept any SSL/TLS certificate, including self-signed certificates.
    /// </summary>
    public bool ValidateAnyCertificate { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout in seconds.
    /// </summary>
    public int? ConnectTimeout { get; set; }

    /// <summary>
    /// Gets or sets the read timeout in seconds.
    /// </summary>
    public int? ReadTimeout { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// </summary>
    public int? RetryAttempts { get; set; }
}
