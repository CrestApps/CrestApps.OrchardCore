using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Sends through Orchard Core's email service, with the tenant's default provider or the one the address names (SMTP,
/// Azure Communication Services or any other provider module). The service cannot set headers, so a reply threads by its
/// <c>Re:</c> subject, which every common mail client honors.
/// </summary>
public sealed class OrchardCoreEmailTransport : IEmailTransport
{
    private readonly IEmailService _emailService;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreEmailTransport"/> class.
    /// </summary>
    /// <param name="emailService">Orchard Core's email service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrchardCoreEmailTransport(
        IEmailService emailService,
        ILogger<OrchardCoreEmailTransport> logger,
        IStringLocalizer<OrchardCoreEmailTransport> stringLocalizer)
    {
        _emailService = emailService;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => EmailChannelConstants.Transports.OrchardCore;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Orchard Core email service"];

    /// <inheritdoc/>
    public bool SupportsHeaders => false;

    /// <inheritdoc/>
    public async Task<MessageDispatchResult> SendAsync(EmailTransportMessage message, EmailAddressSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(settings);

        var mail = new MailMessage
        {
            From = EmailAddressFormatter.Format(message.FromAddress, message.FromName),
            To = message.ToAddress,
            ReplyTo = string.IsNullOrEmpty(message.ReplyToAddress) ? message.FromAddress : message.ReplyToAddress,
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        };

        var streams = new List<MemoryStream>();

        try
        {
            foreach (var attachment in message.Attachments)
            {
                var stream = new MemoryStream(attachment.Content, writable: false);
                streams.Add(stream);

                mail.Attachments.Add(new MailMessageAttachment
                {
                    Filename = attachment.FileName,
                    Stream = stream,
                });
            }

            var providerName = string.IsNullOrWhiteSpace(settings.ProviderName) ? null : settings.ProviderName;
            var result = await _emailService.SendAsync(mail, providerName, cancellationToken);

            if (result.Succeeded)
            {
                // The service gives back no identifier of its own, so the email is recognised by its subject thread.
                return MessageDispatchResult.Success();
            }

            var errors = result.Errors?
                .Select(error => error.Message?.ToString())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Select(error => new LocalizedString(error, error))
                .ToArray() ?? [];

            _logger.LogWarning(
                "Orchard Core's email service refused an email from address provider '{ProviderName}': {Errors}",
                (providerName ?? "(default)").SanitizeLogValue(),
                string.Join("; ", errors.Select(error => error.Value)).SanitizeLogValue());

            return errors.Length > 0
                ? MessageDispatchResult.Failed(errors)
                : MessageDispatchResult.Failed(S["The email could not be sent."]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A provider that throws instead of returning a failure (a misconfigured provider, an unreachable server)
            // is still a refusal the outbox can retry, never an error that takes the caller down.
            _logger.LogError(ex, "Orchard Core's email service failed to send an email.");

            return MessageDispatchResult.Failed(S["The email could not be sent: {0}", ex.Message]);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
