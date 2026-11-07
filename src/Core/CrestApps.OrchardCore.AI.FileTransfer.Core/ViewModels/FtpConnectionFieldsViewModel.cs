using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;

/// <summary>
/// The fields of an FTP or FTPS connection editor. A stored password is never shown; <see cref="HasPassword"/> says whether one is saved.
/// </summary>
public class FtpConnectionFieldsViewModel
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
    [BindNever]
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
