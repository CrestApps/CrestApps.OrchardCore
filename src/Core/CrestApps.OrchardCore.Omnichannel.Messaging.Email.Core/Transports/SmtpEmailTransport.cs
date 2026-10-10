using System.Net.Sockets;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Sends through the address's own SMTP server, the way a help desk connects a mailbox: Microsoft 365, Google
/// Workspace, a hosting provider's mail server or a provider's SMTP relay. It sends the email's own headers, so every
/// reply threads in the customer's mail client and a bounce names the email it bounced.
/// </summary>
public sealed class SmtpEmailTransport : IEmailTransport
{
    private readonly IEmailSecretProtector _secretProtector;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmtpEmailTransport"/> class.
    /// </summary>
    /// <param name="secretProtector">The protector the server password is read with.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SmtpEmailTransport(
        IEmailSecretProtector secretProtector,
        ILogger<SmtpEmailTransport> logger,
        IStringLocalizer<SmtpEmailTransport> stringLocalizer)
    {
        _secretProtector = secretProtector;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Transports.Smtp;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["The address's own SMTP server"];

    /// <inheritdoc/>
    public bool SupportsHeaders => true;

    /// <inheritdoc/>
    public async Task<MessageDispatchResult> SendAsync(EmailTransportMessage message, EmailAddressSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(settings);

        var server = settings.Smtp;

        if (server is null || !server.IsConfigured)
        {
            return MessageDispatchResult.Failed(S["The address sends through its own SMTP server, but no server is set on the address."]);
        }

        var mime = EmailMimeBuilder.Build(message);

        try
        {
            using var client = new SmtpClient();

            await EmailServerConnection.ConnectAsync(
                client,
                server,
                EmailServerConnection.ResolvePort(server, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort),
                _secretProtector,
                cancellationToken);

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            return MessageDispatchResult.Success(mime.MessageId);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted && (int)ex.StatusCode >= 500)
        {
            // The server refused the recipient outright (a 5xx answer): the mailbox does not exist or will not take mail
            // from us. No retry can change that, so the refusal is marked as permanent. A 4xx answer (greylisting, a
            // full mailbox) is temporary and falls through to the retried failure below.
            _logger.LogWarning(
                "The SMTP server {Host} refused the recipient of an email ({StatusCode}).",
                server.Host.SanitizeLogValue(),
                (int)ex.StatusCode);

            return new MessageDispatchResult
            {
                Succeeded = false,
                ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected,
                Errors = [S["The mail server refused the recipient: {0}", ex.Message]],
            };
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning(ex, "The SMTP server {Host} refused the address's sign-in.", server.Host.SanitizeLogValue());

            return MessageDispatchResult.Failed(S["The mail server refused the sign-in. Check the user name and password on the address."]);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or ServiceNotConnectedException or SocketException or IOException or SslHandshakeException or TimeoutException)
        {
            // Anything else is worth another try: the server was busy, unreachable or broke off the conversation.
            _logger.LogWarning(ex, "The SMTP server {Host} did not accept an email.", server.Host.SanitizeLogValue());

            return MessageDispatchResult.Failed(S["The mail server did not accept the email: {0}", ex.Message]);
        }
    }
}
