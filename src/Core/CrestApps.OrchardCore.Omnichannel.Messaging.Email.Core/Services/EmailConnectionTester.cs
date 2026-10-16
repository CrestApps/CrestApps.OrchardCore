using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailConnectionTester"/>.
/// </summary>
public sealed class EmailConnectionTester : IEmailConnectionTester
{
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IEmailMailboxReader _mailboxReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailConnectionTester"/> class.
    /// </summary>
    /// <param name="secretProtector">The protector the passwords are read with.</param>
    /// <param name="mailboxReader">The mailbox reader, which tests the IMAP connection.</param>
    public EmailConnectionTester(
        IEmailSecretProtector secretProtector,
        IEmailMailboxReader mailboxReader)
    {
        _secretProtector = secretProtector;
        _mailboxReader = mailboxReader;
    }

    /// <inheritdoc/>
    public async Task<string> TestSmtpAsync(EmailServerSettings server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (!server.IsConfigured)
        {
            return "No SMTP server is set.";
        }

        try
        {
            using var client = new SmtpClient();

            await EmailServerConnection.ConnectAsync(
                client,
                server,
                EmailServerConnection.ResolvePort(server, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort),
                _secretProtector,
                cancellationToken);

            await client.DisconnectAsync(quit: true, cancellationToken);

            return null;
        }
        catch (AuthenticationException)
        {
            return "The mail server refused the sign-in. Check the user name and password (or app password).";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex.Message;
        }
    }

    /// <inheritdoc/>
    public Task<string> TestImapAsync(EmailMailboxSettings mailbox, CancellationToken cancellationToken = default)
        => _mailboxReader.TestAsync(mailbox, cancellationToken);
}
