using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Ftp.ViewModels;

/// <summary>
/// View model for the FTP/FTPS connector settings.
/// </summary>
public class FtpFileSourceViewModel
{
    /// <summary>
    /// Gets or sets the folder on the server to read.
    /// </summary>
    public string RemoteRootPath { get; set; } = "/";

    /// <summary>
    /// Gets or sets a value indicating whether sub-folders are read too.
    /// </summary>
    public bool RemoteRecursive { get; set; } = true;

    /// <summary>
    /// Gets or sets the most files one listing will take on. Empty uses the host default.
    /// </summary>
    public int? RemoteMaxItems { get; set; }

    /// <summary>
    /// Gets or sets the host.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the port. Empty uses 21.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Gets or sets the password. Left blank on the way out, and a blank submission keeps the stored one.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the encryption mode.
    /// </summary>
    public string EncryptionMode { get; set; }

    /// <summary>
    /// Gets or sets the data connection type.
    /// </summary>
    public string DataConnectionType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any certificate is accepted.
    /// </summary>
    public bool ValidateAnyCertificate { get; set; }

    /// <summary>
    /// Gets or sets the connect timeout, in milliseconds.
    /// </summary>
    public int? ConnectTimeout { get; set; }

    /// <summary>
    /// Gets or sets the read timeout, in milliseconds.
    /// </summary>
    public int? ReadTimeout { get; set; }

    /// <summary>
    /// Gets or sets how many times a failed operation is retried.
    /// </summary>
    public int? RetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets whether a password is already stored, so the editor can say so without sending it.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }
}
