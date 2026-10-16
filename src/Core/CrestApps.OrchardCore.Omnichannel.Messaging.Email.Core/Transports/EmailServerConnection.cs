using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using MailKit;
using MailKit.Security;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Connects and signs a MailKit client in to a mail server described by <see cref="EmailServerSettings"/>, the same way
/// for the SMTP server an address sends through and the IMAP server its mailbox is read from.
/// </summary>
public static class EmailServerConnection
{
    /// <summary>
    /// The default SMTP submission port with STARTTLS.
    /// </summary>
    public const int SmtpSubmissionPort = 587;

    /// <summary>
    /// The default SMTP port with TLS from the start.
    /// </summary>
    public const int SmtpImplicitTlsPort = 465;

    /// <summary>
    /// The default IMAP port with STARTTLS.
    /// </summary>
    public const int ImapPort = 143;

    /// <summary>
    /// The default IMAP port with TLS from the start.
    /// </summary>
    public const int ImapImplicitTlsPort = 993;

    /// <summary>
    /// The longest a mail server is given to answer, in milliseconds.
    /// </summary>
    public const int TimeoutMilliseconds = 30_000;

    /// <summary>
    /// Maps the configured security onto MailKit's options.
    /// </summary>
    /// <param name="security">The configured security.</param>
    /// <returns>The MailKit socket options.</returns>
    public static SecureSocketOptions ToSocketOptions(EmailConnectionSecurity security)
        => security switch
        {
            EmailConnectionSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            EmailConnectionSecurity.StartTls => SecureSocketOptions.StartTls,
            EmailConnectionSecurity.None => SecureSocketOptions.None,
            _ => SecureSocketOptions.Auto,
        };

    /// <summary>
    /// Resolves the port to connect to: the configured one, or the protocol's default for the configured security.
    /// </summary>
    /// <param name="server">The server settings.</param>
    /// <param name="defaultPort">The protocol's default port.</param>
    /// <param name="implicitTlsPort">The protocol's implicit-TLS port.</param>
    /// <returns>The port.</returns>
    public static int ResolvePort(EmailServerSettings server, int defaultPort, int implicitTlsPort)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.Port > 0)
        {
            return server.Port;
        }

        return server.Security == EmailConnectionSecurity.SslOnConnect
            ? implicitTlsPort
            : defaultPort;
    }

    /// <summary>
    /// Connects a client and signs it in when the server settings name a user.
    /// </summary>
    /// <param name="client">The MailKit client.</param>
    /// <param name="server">The server settings.</param>
    /// <param name="port">The port to connect to.</param>
    /// <param name="secretProtector">The protector the password is read with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task ConnectAsync(
        IMailService client,
        EmailServerSettings server,
        int port,
        IEmailSecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(secretProtector);

        client.Timeout = TimeoutMilliseconds;

        await client.ConnectAsync(server.Host.Trim(), port, ToSocketOptions(server.Security), cancellationToken);

        if (!string.IsNullOrWhiteSpace(server.UserName))
        {
            var password = secretProtector.Unprotect(server.Password) ?? string.Empty;

            await client.AuthenticateAsync(server.UserName.Trim(), password, cancellationToken);
        }
    }
}
