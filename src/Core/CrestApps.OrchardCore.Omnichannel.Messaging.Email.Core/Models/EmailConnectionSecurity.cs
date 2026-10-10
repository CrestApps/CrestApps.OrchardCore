namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// How the connection to a mail server is secured.
/// </summary>
public enum EmailConnectionSecurity
{
    /// <summary>
    /// Pick by port: TLS from the start on the implicit-TLS ports (465, 993, 995), STARTTLS when the server offers it
    /// on the others.
    /// </summary>
    Auto,

    /// <summary>
    /// TLS from the start of the connection (implicit TLS), as on port 465 for SMTP and 993 for IMAP.
    /// </summary>
    SslOnConnect,

    /// <summary>
    /// A plain connection upgraded with STARTTLS, which must succeed, as on port 587 for SMTP and 143 for IMAP.
    /// </summary>
    StartTls,

    /// <summary>
    /// No encryption. Only for a server on a trusted network, such as a local relay.
    /// </summary>
    None,
}
