using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailDispatcher"/>.
/// </summary>
public sealed class EmailDispatcher : IEmailDispatcher
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IEnumerable<IEmailTransport> _transports;
    private readonly IMessagingAttachmentStore _attachmentStore;
    private readonly IEmailUnsubscribeLinks _unsubscribeLinks;
    private readonly IEmailSendingGovernor _governor;
    private readonly ISession _session;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailDispatcher"/> class.
    /// </summary>
    /// <param name="endpointManager">The manager of the business's addresses.</param>
    /// <param name="transports">The registered sending transports.</param>
    /// <param name="attachmentStore">The store the files are read from.</param>
    /// <param name="unsubscribeLinks">The builder of unsubscribe links.</param>
    /// <param name="governor">The governor of the address's limits, pauses and suppression list.</param>
    /// <param name="session">The session the conversation's earlier emails are read from.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailDispatcher(
        IOmnichannelChannelEndpointManager endpointManager,
        IEnumerable<IEmailTransport> transports,
        IMessagingAttachmentStore attachmentStore,
        IEmailUnsubscribeLinks unsubscribeLinks,
        IEmailSendingGovernor governor,
        ISession session,
        ILogger<EmailDispatcher> logger,
        IStringLocalizer<EmailDispatcher> stringLocalizer)
    {
        _endpointManager = endpointManager;
        _transports = transports;
        _attachmentStore = attachmentStore;
        _unsubscribeLinks = unsubscribeLinks;
        _governor = governor;
        _session = session;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<MessageDispatchResult> SendAsync(MessagingOutboundMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var serviceAddress = OmnichannelEmailAddress.Normalize(message.ServiceAddress);
        var contactAddress = OmnichannelEmailAddress.Normalize(message.ContactAddress);

        if (!OmnichannelEmailAddress.IsValid(serviceAddress) || !OmnichannelEmailAddress.IsValid(contactAddress))
        {
            return MessageDispatchResult.Failed(S["An email needs a valid sending address and a valid recipient."]);
        }

        var endpoint = await _endpointManager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, serviceAddress, cancellationToken);

        if (endpoint is null)
        {
            return MessageDispatchResult.Failed(S["{0} is not one of the business's email addresses, or it is not used for email.", serviceAddress]);
        }

        var settings = endpoint.TryGet<EmailAddressSettings>(out var stored) ? stored : new EmailAddressSettings();
        var transportName = string.IsNullOrWhiteSpace(settings.TransportName) ? EmailChannelConstants.Transports.OrchardCore : settings.TransportName;
        var transport = _transports.FirstOrDefault(candidate => string.Equals(candidate.Name, transportName, StringComparison.OrdinalIgnoreCase));

        if (transport is null)
        {
            _logger.LogWarning(
                "The email address '{AddressId}' sends through the '{Transport}' transport, which no enabled feature provides.",
                endpoint.ItemId.SanitizeLogValue(),
                transportName.SanitizeLogValue());

            return MessageDispatchResult.Failed(S["The address sends through '{0}', which is not available. Pick another way to send on the address.", transportName]);
        }

        var senderName = string.IsNullOrWhiteSpace(settings.SenderName) ? endpoint.DisplayText : settings.SenderName.Trim();
        var isBulk = message.Purpose is MessagingOutboundPurpose.Broadcast or MessagingOutboundPurpose.Outreach;

        // Nothing goes to a suppressed address; bulk mail also waits while the address is at its limits or paused. A
        // held-back email is a deferral the caller reschedules, not a failure.
        var decision = await _governor.EvaluateAsync(endpoint, settings, contactAddress, isBulk, reserveTurn: true, cancellationToken);

        if (decision.IsRefused)
        {
            return new MessageDispatchResult
            {
                Succeeded = false,
                ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected,
                Errors = [decision.Reason],
            };
        }

        if (!decision.IsAllowed)
        {
            return MessageDispatchResult.Deferred(decision.RetryAfterUtc.Value, decision.Reason);
        }

        // A reply threads under the customer's last email; bulk and outreach mail starts a thread of its own.
        var replyTo = isBulk ? null : await FindReplyToAsync(message, cancellationToken);
        var replyMetadata = replyTo?.GetEmailMetadata();

        var subject = MessagingSubjects.Clean(message.Subject)
            ?? MessagingSubjects.ForReply(replyTo?.GetSubject())
            ?? MessagingSubjects.Clean(settings.DefaultSubject)
            ?? MessagingSubjects.Clean(S["Message from {0}", senderName ?? serviceAddress].Value);

        var unsubscribeUrl = isBulk && settings.IncludeUnsubscribeLink
            ? await _unsubscribeLinks.CreateUrlAsync(contactAddress, serviceAddress, cancellationToken)
            : null;

        var unsubscribeText = S["Unsubscribe from these emails"].Value;

        var transportMessage = new EmailTransportMessage
        {
            FromAddress = serviceAddress,
            FromName = senderName,
            ToAddress = contactAddress,
            ReplyToAddress = serviceAddress,
            Subject = subject,
            TextBody = EmailBodyFormatter.ToText(message.Body, settings.Signature, unsubscribeUrl, unsubscribeText),
            HtmlBody = EmailBodyFormatter.ToHtml(message.Body, settings.Signature, unsubscribeUrl, unsubscribeText),
            MessageId = CreateMessageId(serviceAddress),
            InReplyTo = replyMetadata?.MessageId,
            References = BuildReferences(replyMetadata),
            Attachments = await ReadAttachmentsAsync(message.Attachments, cancellationToken),
        };

        ApplyHeaders(transportMessage, message.Purpose, unsubscribeUrl);

        var result = await transport.SendAsync(transportMessage, settings, cancellationToken);

        if (result.Succeeded)
        {
            await _governor.RecordSentAsync(endpoint, contactAddress, result.ProviderMessageId ?? transportMessage.MessageId, isBulk, cancellationToken);
        }
        else
        {
            result = await RecordFailureAsync(endpoint, settings, contactAddress, isBulk, result, cancellationToken);
        }

        if (result.Succeeded && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Sent a {Purpose} email from address '{AddressId}' through the '{Transport}' transport{Threading}.",
                message.Purpose,
                endpoint.ItemId.SanitizeLogValue(),
                transport.Name,
                replyMetadata?.MessageId is null ? string.Empty : " as a threaded reply");
        }

        return result;
    }

    // Reads what the server's refusal means and acts on it: a dead address is suppressed and never retried, a block or
    // a throttle pauses the address's bulk mail, and a throttled bulk email waits for the pause instead of spending a
    // retry.
    private async Task<MessageDispatchResult> RecordFailureAsync(
        OmnichannelChannelEndpoint endpoint,
        EmailAddressSettings settings,
        string contactAddress,
        bool isBulk,
        MessageDispatchResult result,
        CancellationToken cancellationToken)
    {
        var detail = result.GetErrorText();
        var kind = EmailFailureClassifier.Classify(result.ErrorCode, detail);
        var pausedUntil = await _governor.RecordFailureAsync(endpoint, settings, contactAddress, kind, detail, isBulk, cancellationToken);

        switch (kind)
        {
            case EmailFailureKind.HardBounce:
                result.ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected;
                break;

            case EmailFailureKind.Blocked:
                result.ErrorCode = OmnichannelConstants.MessagingErrorCodes.SenderBlocked;
                break;

            case EmailFailureKind.Throttled when isBulk && pausedUntil is not null:
                return new MessageDispatchResult
                {
                    Succeeded = false,
                    RetryAfterUtc = pausedUntil,
                    ErrorCode = OmnichannelConstants.MessagingErrorCodes.Deferred,
                    Errors = result.Errors,
                };
        }

        return result;
    }

    // The message being answered: the one the sender named, or the newest email the customer sent in the conversation.
    private async Task<OmnichannelMessage> FindReplyToAsync(MessagingOutboundMessage message, CancellationToken cancellationToken)
    {
        if (message.ReplyTo is not null)
        {
            return message.ReplyTo;
        }

        if (string.IsNullOrEmpty(message.ConversationId))
        {
            return null;
        }

        var conversationId = message.ConversationId;

        return await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId && index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IList<EmailTransportAttachment>> ReadAttachmentsAsync(IList<MessagingAttachment> attachments, CancellationToken cancellationToken)
    {
        var files = new List<EmailTransportAttachment>();

        foreach (var attachment in attachments ?? [])
        {
            if (string.IsNullOrEmpty(attachment?.Id))
            {
                continue;
            }

            var content = await _attachmentStore.ReadAsync(attachment.Id, cancellationToken);

            if (content is null)
            {
                _logger.LogWarning("An attachment of an outbound email was not found in the attachment store and was left out.");

                continue;
            }

            files.Add(new EmailTransportAttachment
            {
                FileName = string.IsNullOrWhiteSpace(attachment.FileName)
                    ? $"attachment{MessagingFileFormats.FindByContentType(attachment.ContentType)?.PreferredExtension}"
                    : attachment.FileName,
                ContentType = attachment.ContentType,
                Content = content,
            });
        }

        return files;
    }

    // The headers that tell mail systems what kind of email this is. An automatic reply says so (RFC 3834), so the
    // customer's own auto-responder does not answer it and start a loop; bulk mail says so, so out-of-office replies are
    // not sent to it; and bulk and outreach mail offer the one-click unsubscribe the large mailbox providers require.
    private static void ApplyHeaders(EmailTransportMessage message, MessagingOutboundPurpose purpose, string unsubscribeUrl)
    {
        switch (purpose)
        {
            case MessagingOutboundPurpose.AutoReply:
            case MessagingOutboundPurpose.Automation:
                message.Headers["Auto-Submitted"] = "auto-replied";
                message.Headers["X-Auto-Response-Suppress"] = "All";
                break;
            case MessagingOutboundPurpose.Outreach:
                message.Headers["Auto-Submitted"] = "auto-generated";
                message.Headers["X-Auto-Response-Suppress"] = "OOF, AutoReply";
                break;
            case MessagingOutboundPurpose.Broadcast:
                message.Headers["Precedence"] = "bulk";
                message.Headers["X-Auto-Response-Suppress"] = "OOF, AutoReply";
                break;
        }

        if (!string.IsNullOrEmpty(unsubscribeUrl))
        {
            message.Headers["List-Unsubscribe"] = $"<{unsubscribeUrl}>";
            message.Headers["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click";
        }
    }

    private static List<string> BuildReferences(EmailMessageMetadata replyMetadata)
    {
        if (replyMetadata is null)
        {
            return [];
        }

        var references = (replyMetadata.References ?? [])
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .ToList();

        if (!string.IsNullOrWhiteSpace(replyMetadata.MessageId) &&
            !references.Contains(replyMetadata.MessageId, StringComparer.Ordinal))
        {
            references.Add(replyMetadata.MessageId);
        }

        // A long thread's references are kept to the first and the most recent ones, as mail clients do (RFC 5322
        // allows trimming), so the header stays a reasonable size.
        return references.Count <= 10
            ? references
            : references.Take(1).Concat(references.Skip(references.Count - 9)).ToList();
    }

    private static string CreateMessageId(string fromAddress)
    {
        var at = fromAddress.LastIndexOf('@');
        var domain = at >= 0 ? fromAddress.Substring(at + 1) : "localhost";

        return $"{UniqueId.GenerateId()}@{domain}";
    }
}
