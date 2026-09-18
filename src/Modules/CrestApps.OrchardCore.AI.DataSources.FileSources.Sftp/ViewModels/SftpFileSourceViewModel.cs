using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Sftp.ViewModels;

/// <summary>
/// View model for the SFTP connector settings.
/// </summary>
public class SftpFileSourceViewModel
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
    /// Gets or sets the port. Empty uses 22.
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
    /// Gets or sets the private key. Left blank on the way out, and a blank submission keeps the stored one.
    /// </summary>
    public string PrivateKey { get; set; }

    /// <summary>
    /// Gets or sets the private key passphrase. Left blank on the way out, and a blank submission keeps the
    /// stored one.
    /// </summary>
    public string Passphrase { get; set; }

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
    /// Gets or sets the proxy password. Left blank on the way out, and a blank submission keeps the stored
    /// one.
    /// </summary>
    public string ProxyPassword { get; set; }

    /// <summary>
    /// Gets or sets the connection timeout, in milliseconds.
    /// </summary>
    public int? ConnectionTimeout { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive interval, in milliseconds.
    /// </summary>
    public int? KeepAliveInterval { get; set; }

    /// <summary>
    /// Gets or sets whether a password is already stored.
    /// </summary>
    [BindNever]
    public bool HasPassword { get; set; }

    /// <summary>
    /// Gets or sets whether a private key is already stored.
    /// </summary>
    [BindNever]
    public bool HasPrivateKey { get; set; }

    /// <summary>
    /// Gets or sets whether a passphrase is already stored.
    /// </summary>
    [BindNever]
    public bool HasPassphrase { get; set; }

    /// <summary>
    /// Gets or sets whether a proxy password is already stored.
    /// </summary>
    [BindNever]
    public bool HasProxyPassword { get; set; }
}
