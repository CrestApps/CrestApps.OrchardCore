namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// The connection to one mail server: the SMTP server an address sends through, or the IMAP server its mailbox is read
/// from.
/// </summary>
public sealed class EmailServerSettings
{
    /// <summary>
    /// Gets or sets the server's host name.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the server's port, or zero for the protocol's default for the chosen security.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets how the connection is secured.
    /// </summary>
    public EmailConnectionSecurity Security { get; set; }

    /// <summary>
    /// Gets or sets the user name to sign in with; empty to connect without signing in.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the password, protected with the tenant's data protection keys. Never stored in clear text.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets a value indicating whether a host is set, which is what makes the connection usable.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
