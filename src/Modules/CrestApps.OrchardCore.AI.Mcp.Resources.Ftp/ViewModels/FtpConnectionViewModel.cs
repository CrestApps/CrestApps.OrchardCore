namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.ViewModels;

/// <summary>
/// Represents the view model for ftp connection.
/// </summary>
public class FtpConnectionViewModel
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
    /// Gets or sets the encryption mode.
    /// </summary>
    public string EncryptionMode { get; set; }

    /// <summary>
    /// Gets or sets the data connection type.
    /// </summary>
    public string DataConnectionType { get; set; }

    /// <summary>
    /// Gets or sets the validate any certificate.
    /// </summary>
    public bool ValidateAnyCertificate { get; set; }

    /// <summary>
    /// Gets or sets the connect timeout.
    /// </summary>
    public int? ConnectTimeout { get; set; }

    /// <summary>
    /// Gets or sets the read timeout.
    /// </summary>
    public int? ReadTimeout { get; set; }

    /// <summary>
    /// Gets or sets the retry attempts.
    /// </summary>
    public int? RetryAttempts { get; set; }
}
