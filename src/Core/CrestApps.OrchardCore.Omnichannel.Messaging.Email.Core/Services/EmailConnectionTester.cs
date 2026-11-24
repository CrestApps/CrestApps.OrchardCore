using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailConnectionTester"/>.
/// </summary>
public sealed class EmailConnectionTester : IEmailConnectionTester
{
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IEmailMailboxReader _mailboxReader;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailConnectionTester"/> class.
    /// </summary>
    /// <param name="secretProtector">The protector the passwords are read with.</param>
    /// <param name="mailboxReader">The mailbox reader, which tests the IMAP connection.</param>
    /// <param name="logger">The logger the reason a test failed is written to.</param>
    public EmailConnectionTester(
        IEmailSecretProtector secretProtector,
        IEmailMailboxReader mailboxReader,
        ILogger<EmailConnectionTester> logger)
    {
        _secretProtector = secretProtector;
        _mailboxReader = mailboxReader;
        _logger = logger;
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The person sees only whether the sign-in was refused; what went wrong exactly is for the log, so the test
            // cannot be used to find out which hosts and ports answer on the site's network.
            _logger.LogWarning(ex, "Testing the SMTP server '{Host}' failed.", server.Host.SanitizeLogValue());

            return EmailServerConnection.DescribeFailure(ex);
        }
    }

    /// <inheritdoc/>
    public Task<string> TestImapAsync(EmailMailboxSettings mailbox, CancellationToken cancellationToken = default)
        => _mailboxReader.TestAsync(mailbox, cancellationToken);
}
