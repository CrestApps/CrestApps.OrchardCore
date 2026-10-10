using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// The default <see cref="IEmailInboundReceiver"/>.
/// </summary>
public sealed class EmailInboundReceiver : IEmailInboundReceiver
{
    /// <summary>
    /// The provider name every inbound email is recorded under in the durable inbox, whichever source it came from, so
    /// an email that reaches the business by two routes (its mailbox and a webhook) is still received once.
    /// </summary>
    public const string InboxProviderName = "email";

    private const int MaxAttachments = 10;

    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IMessagingAttachmentStore _attachmentStore;
    private readonly IEnumerable<IProviderWebhookInbox> _inboxes;
    private readonly IEnumerable<IProviderWebhookInboxHandler> _inboxHandlers;
    private readonly IMessagingConversationService _conversationService;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailInboundReceiver"/> class.
    /// </summary>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="attachmentStore">The store received files are kept in.</param>
    /// <param name="inboxes">The durable provider inbox, when the feature that provides it is enabled.</param>
    /// <param name="inboxHandlers">The inbox handlers, used directly when there is no inbox.</param>
    /// <param name="conversationService">The service bounces are applied to the bounced email through.</param>
    /// <param name="session">The session bounced emails are looked up in.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public EmailInboundReceiver(
        IOmnichannelChannelEndpointManager endpointManager,
        IMessagingAttachmentStore attachmentStore,
        IEnumerable<IProviderWebhookInbox> inboxes,
        IEnumerable<IProviderWebhookInboxHandler> inboxHandlers,
        IMessagingConversationService conversationService,
        ISession session,
        IClock clock,
        ILogger<EmailInboundReceiver> logger)
    {
        _endpointManager = endpointManager;
        _attachmentStore = attachmentStore;
        _inboxes = inboxes;
        _inboxHandlers = inboxHandlers;
        _conversationService = conversationService;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<EmailInboundResult> ReceiveAsync(InboundEmail email, string source, bool dispatch = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var messageId = ResolveMessageId(email);

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["EmailMessageId"] = messageId.SanitizeLogValue(),
            ["EmailSource"] = source.SanitizeLogValue(),
        });

        // A bounce is about an email we sent, not a message from a customer. It marks that email as failed, and nothing
        // else: it never opens a conversation and is never answered.
        if (email.DeliveryReport is not null)
        {
            await ApplyBounceAsync(email.DeliveryReport, cancellationToken);

            return EmailInboundResult.Of(EmailInboundStatus.Ignored, "The email is a delivery report (bounce).");
        }

        var from = email.From?.Address;

        if (!OmnichannelEmailAddress.IsValid(from))
        {
            _logger.LogWarning("An inbound email from {Source} has no usable sender address and was not received.", source.SanitizeLogValue());

            return EmailInboundResult.Of(EmailInboundStatus.Ignored, "The email has no sender address.");
        }

        var endpoint = await FindRecipientAddressAsync(email, cancellationToken);

        if (endpoint is null)
        {
            _logger.LogWarning(
                "An inbound email from {Source} was sent to {RecipientCount} address(es), none of which is one of the business's email addresses used for email.",
                source.SanitizeLogValue(),
                email.DeliveredTo.Count + email.To.Count + email.Cc.Count);

            return EmailInboundResult.Of(EmailInboundStatus.UnknownAddress, "None of the recipients is one of the business's email addresses.");
        }

        // Mail from one of our own addresses is our own mail coming back (a copy, a loop between two of our mailboxes);
        // receiving it would have the workspace, or worse the AI, answer itself.
        if (await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, from, cancellationToken) is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("An inbound email from {Source} was sent by one of the business's own addresses and was not received.", source.SanitizeLogValue());
            }

            return EmailInboundResult.Of(EmailInboundStatus.Ignored, "The email was sent by one of the business's own addresses.");
        }

        var message = await CreateMessageAsync(email, messageId, endpoint, cancellationToken);
        var payload = JsonSerializer.Serialize(message);
        var inbox = _inboxes.FirstOrDefault();

        if (inbox is null)
        {
            // No durable inbox on this tenant: the email is handled inline, as it would be from storage.
            await DispatchInlineAsync(payload, cancellationToken);

            return new EmailInboundResult { Status = EmailInboundStatus.Accepted, AddressId = endpoint.ItemId };
        }

        var acceptance = await inbox.AcceptAsync(
            new ProviderWebhookInboxDelivery
            {
                ProviderName = InboxProviderName,
                DeliveryId = ToDeliveryId(messageId),
                HandlerName = EmailChannelConstants.InboxHandlerName,
                Payload = payload,
            },
            cancellationToken);

        switch (acceptance.Status)
        {
            case ProviderWebhookInboxAcceptanceStatus.Duplicate:
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("An inbound email from {Source} was received before and was not recorded again.", source.SanitizeLogValue());
                }

                return new EmailInboundResult { Status = EmailInboundStatus.Duplicate, AddressId = endpoint.ItemId };

            case ProviderWebhookInboxAcceptanceStatus.Busy:
                return new EmailInboundResult { Status = EmailInboundStatus.Busy, AddressId = endpoint.ItemId, Reason = "The same email is being received by another call." };
        }

        if (dispatch)
        {
            try
            {
                await inbox.DispatchAsync(acceptance.MessageId, cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // Another node claimed the same delivery. The inbox retries it from storage, so the email is not lost.
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Received an inbound email from {Source} on address '{AddressId}' ({AttachmentCount} attachment(s), automatic: {IsAutoGenerated}).",
                source.SanitizeLogValue(),
                endpoint.ItemId.SanitizeLogValue(),
                message.GetAttachments().Count,
                message.IsAutoGeneratedEmail());
        }

        return new EmailInboundResult { Status = EmailInboundStatus.Accepted, AddressId = endpoint.ItemId, InboxMessageId = acceptance.MessageId };
    }

    private async Task<OmnichannelChannelEndpoint> FindRecipientAddressAsync(InboundEmail email, CancellationToken cancellationToken)
    {
        // The delivery address names our mailbox even for a Bcc or a forwarding alias, so it is asked first.
        var candidates = email.DeliveredTo
            .Concat(email.To.Select(address => address.Address))
            .Concat(email.Cc.Select(address => address.Address))
            .Select(OmnichannelEmailAddress.Normalize)
            .Where(address => !string.IsNullOrEmpty(address))
            .Distinct(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var endpoint = await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, candidate, cancellationToken);

            if (endpoint is not null)
            {
                return endpoint;
            }
        }

        return null;
    }

    private async Task<OmnichannelMessage> CreateMessageAsync(InboundEmail email, string messageId, OmnichannelChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        var body = !string.IsNullOrWhiteSpace(email.TextBody)
            ? email.TextBody
            : EmailHtmlToText.Convert(email.HtmlBody);

        var (reply, quoted) = EmailReplyText.Split(body);

        // A provider that already separated the reply knows the sender's mail client better than a heuristic does.
        if (!string.IsNullOrWhiteSpace(email.StrippedReply))
        {
            reply = email.StrippedReply.Trim();
        }

        var message = new OmnichannelMessage
        {
            Id = UniqueId.GenerateId(),
            Channel = OmnichannelConstants.Channels.Email,
            CustomerAddress = email.From.Address,
            ServiceAddress = endpoint.Value,
            Content = Truncate(reply),
            CreatedUtc = _clock.UtcNow,
            IsInbound = true,
            ProviderMessageId = messageId,
        };

        message.SetSubject(email.Subject);
        message.SetQuotedText(Truncate(quoted));

        message.Put(new EmailMessageMetadata
        {
            MessageId = messageId,
            InReplyTo = email.InReplyTo,
            References = email.References.Where(reference => !string.IsNullOrWhiteSpace(reference)).ToList(),
            FromName = email.From.Name,
            Cc = email.To
                .Concat(email.Cc)
                .Select(address => address.Address)
                .Where(address => !string.IsNullOrEmpty(address) && !string.Equals(address, endpoint.Value, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            IsAutoGenerated = EmailAutoGeneratedDetector.IsAutoGenerated(email),
        });

        await StoreAttachmentsAsync(email, message, cancellationToken);

        return message;
    }

    // The files are kept now, while the email is in hand: unlike a picture message, an email carries its files itself
    // and nothing can download them later. An inline picture the HTML body shows (a logo in a signature) is left out.
    private async Task StoreAttachmentsAsync(InboundEmail email, OmnichannelMessage message, CancellationToken cancellationToken)
    {
        var stored = new List<MessagingAttachment>();
        var skipped = 0;
        var totalBytes = 0L;
        var index = 0;

        foreach (var file in email.Attachments)
        {
            if (file?.Content is null || file.Content.Length == 0 || IsInlineImage(file, email.HtmlBody))
            {
                continue;
            }

            var format = MessagingFileFormats.Detect(file.Content, file.FileName, file.ContentType, MessagingFileFormats.All);

            if (format is null || stored.Count >= MaxAttachments || totalBytes + file.Content.Length > EmailChannelConstants.MaxInboundEmailBytes)
            {
                skipped++;

                continue;
            }

            var attachment = new MessagingAttachment
            {
                Id = CreateAttachmentId(message.ProviderMessageId, index++),
                ContentType = format.ContentType,
                FileName = string.IsNullOrWhiteSpace(file.FileName) ? null : Path.GetFileName(file.FileName),
                Length = file.Content.Length,
            };

            await _attachmentStore.StoreAsync(attachment.Id, file.Content, cancellationToken);

            stored.Add(attachment);
            totalBytes += file.Content.Length;
        }

        var part = message.GetOrCreate<MessagingMessageAttachments>();

        part.Items = stored;
        part.SkippedCount = skipped;

        // The files are already kept, so the workspace's media ingestion has nothing to fetch.
        part.Ingested = true;
        message.Put(part);
    }

    private static bool IsInlineImage(InboundEmailAttachment file, string html)
        => !string.IsNullOrEmpty(file.ContentId) &&
            !string.IsNullOrEmpty(html) &&
            html.Contains($"cid:{file.ContentId}", StringComparison.OrdinalIgnoreCase);

    private async Task ApplyBounceAsync(InboundEmailDeliveryReport report, CancellationToken cancellationToken)
    {
        if (!report.IsPermanentFailure || string.IsNullOrEmpty(report.OriginalMessageId))
        {
            return;
        }

        var originalId = report.OriginalMessageId;

        var original = await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == OmnichannelConstants.Channels.Email && index.ProviderMessageId == originalId && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (original is null)
        {
            return;
        }

        await _conversationService.ApplyDeliveryReceiptAsync(new MessageDeliveryReceipt
        {
            Channel = OmnichannelConstants.Channels.Email,
            ServiceAddress = original.ServiceAddress,
            ContactAddress = original.CustomerAddress,
            ProviderMessageId = originalId,
            Status = MessageDeliveryStatus.Failed,
            ErrorCode = string.IsNullOrWhiteSpace(report.Diagnostic) ? report.Status : report.Diagnostic,
        }, cancellationToken);

        _logger.LogWarning("An email the business sent bounced ({Status}); the message was marked as failed.", (report.Status ?? report.Action).SanitizeLogValue());
    }

    private async Task DispatchInlineAsync(string payload, CancellationToken cancellationToken)
    {
        var handler = _inboxHandlers.FirstOrDefault(candidate => candidate.TechnicalName == EmailChannelConstants.InboxHandlerName)
            ?? throw new InvalidOperationException("The inbound email handler is not registered.");

        await handler.HandleAsync(payload, cancellationToken);
    }

    // The email's own Message-ID identifies it across redeliveries and sources. An email without one (rare, and only
    // from broken senders) is identified by what it says, so a redelivery of it is still recognised.
    private static string ResolveMessageId(InboundEmail email)
    {
        if (!string.IsNullOrWhiteSpace(email.MessageId))
        {
            return email.MessageId;
        }

        var fingerprint = string.Join('\n', email.From?.Address, email.Subject, email.Date?.ToUnixTimeSeconds(), email.TextBody ?? email.HtmlBody);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));

        return $"{hash.Substring(0, 40)}@generated.invalid";
    }

    private static string ToDeliveryId(string messageId)
        => messageId.Length <= 200
            ? messageId
            : $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(messageId)))}";

    private static string CreateAttachmentId(string providerMessageId, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{OmnichannelConstants.Channels.Email}|{providerMessageId}|{index}"));

        return $"in-{Convert.ToHexStringLower(hash).Substring(0, 40)}";
    }

    private static string Truncate(string text)
        => string.IsNullOrEmpty(text) || text.Length <= EmailChannelConstants.MaxBodyLength
            ? text
            : text.Substring(0, EmailChannelConstants.MaxBodyLength);
}
