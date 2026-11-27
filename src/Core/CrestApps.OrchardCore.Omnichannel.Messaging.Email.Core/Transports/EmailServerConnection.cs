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
    /// The words shown when a mail server refused the sign-in.
    /// </summary>
    internal const string RefusedSignInMessage = "The mail server refused the sign-in. Check the user name and password (or app password).";

    /// <summary>
    /// The words shown for any other failure to connect to a mail server or sign in.
    /// </summary>
    internal const string UnreachableMessage = "The mail server could not be reached, or it did not answer as a mail server. Check the host, port and security; the details are in the site's log.";

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
    /// Determines whether connection settings name the same account on the same server as the stored ones: the same host,
    /// port, security and user name.
    /// </summary>
    /// <remarks>
    /// A stored password is only ever sent to the account it was saved for. Otherwise anybody who may edit an address
    /// could point it at a server of their own, or turn its security off, and have the site send them the password.
    /// Ports are compared once resolved, so a port left at 0 and the default port typed out are the same port.
    /// </remarks>
    /// <param name="stored">The stored settings.</param>
    /// <param name="candidate">The settings as typed.</param>
    /// <param name="defaultPort">The protocol's default port.</param>
    /// <param name="implicitTlsPort">The protocol's implicit-TLS port.</param>
    /// <returns><see langword="true"/> when the stored password may be used with <paramref name="candidate"/>.</returns>
    public static bool IsSameAccount(EmailServerSettings stored, EmailServerSettings candidate, int defaultPort, int implicitTlsPort)
    {
        if (stored is null || candidate is null || !stored.IsConfigured || !candidate.IsConfigured)
        {
            return false;
        }

        return string.Equals(stored.Host.Trim(), candidate.Host.Trim(), StringComparison.OrdinalIgnoreCase) &&
            ResolvePort(stored, defaultPort, implicitTlsPort) == ResolvePort(candidate, defaultPort, implicitTlsPort) &&
            stored.Security == candidate.Security &&
            string.Equals(stored.UserName?.Trim() ?? string.Empty, candidate.UserName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Describes why connecting or signing in to a mail server failed, in words that are safe to show to whoever typed
    /// the settings.
    /// </summary>
    /// <remarks>
    /// Only a refused sign-in is told apart. Every other failure gets the same words, because the exception's own message
    /// (connection refused, no such host, an unexpected greeting) would tell whether a host and port answer, which would
    /// turn the connection test into a probe of the site's own network. The exception belongs in the log.
    /// </remarks>
    /// <param name="exception">The failure.</param>
    /// <returns>The words to show.</returns>
    public static string DescribeFailure(Exception exception)
        => exception is AuthenticationException
            ? RefusedSignInMessage
            : UnreachableMessage;

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
            // "Auto" uses STARTTLS only when the server offers it, so someone in the network path could strip the offer
            // and read the password. The password is only sent in the clear when the admin chose no security.
            if (!client.IsSecure && server.Security != EmailConnectionSecurity.None)
            {
                await client.DisconnectAsync(true, cancellationToken);

                throw new InvalidOperationException($"The mail server at '{server.Host.Trim()}' did not offer an encrypted connection, so the password was not sent. Choose STARTTLS or SSL/TLS, or None to send it unencrypted.");
            }

            var password = secretProtector.Unprotect(server.Password) ?? string.Empty;

            await client.AuthenticateAsync(server.UserName.Trim(), password, cancellationToken);
        }
    }
}
