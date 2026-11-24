using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.PersonalData;
using OrchardCore.Entities;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.PersonalData;

/// <summary>
/// Exports and anonymizes what the email channel keeps about a person, found by their email addresses: the emails
/// exchanged with them (body, subject, quoted history, the names and addresses copied, and the attached files), the
/// delivery log of the mail sent to them, and their entry on the suppression list.
/// </summary>
/// <remarks>
/// <para>
/// Erasing keeps each email as an empty record, so a conversation's message count and response times still add up,
/// but clears its body, subject and quoted history, the sender's name, the addresses copied and the address it was
/// exchanged with, and deletes its attached files from the attachment store. A delivery log entry keeps its sending
/// address, kind, time and receiving domain, which sending limits and sending health are counted from, and loses the
/// recipient and the server's reply, which can quote the address.
/// </para>
/// <para>
/// A suppression keeps its address. The address is the least that must be kept for the business to honour a
/// complaint or a permanent bounce: removing it would let the next broadcast mail the person again, which is the
/// objection the suppression records. Only what the server or provider said, which can quote the address and the
/// person's mailbox details, is cleared.
/// </para>
/// <para>
/// The AI conversations an automated email activity held with the person are erased with the activity, by the CRM's
/// activity contributor.
/// </para>
/// </remarks>
internal sealed class EmailPersonalDataContributor : IPersonalDataContributor
{
    public const string MessageKind = "EmailMessage";

    public const string DeliveryLogKind = "EmailDeliveryLogEntry";

    public const string SuppressionKind = "EmailSuppression";

    // A privacy request reads at most this many records of each kind.
    private const int MaxRecords = 5_000;

    private const string DeliverabilityCollection = EmailChannelConstants.DeliverabilityCollectionName;

    private readonly ISession _session;
    private readonly IMessagingAttachmentStore _attachmentStore;

    public EmailPersonalDataContributor(
        ISession session,
        IMessagingAttachmentStore attachmentStore)
    {
        _session = session;
        _attachmentStore = attachmentStore;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var addresses = OmnichannelEmailAddress.WithCanonicalForms(context.Subject.Emails);

        if (addresses.Length == 0)
        {
            return;
        }

        foreach (var message in await ListMessagesAsync(addresses, cancellationToken))
        {
            var subject = message.GetSubject();
            var metadata = message.GetEmailMetadata();
            var record = context.Add(MessageKind, message.Id, subject ?? EmailChannelConstants.ChannelName, message.CreatedUtc);

            record.Fields[nameof(OmnichannelMessage.IsInbound)] = message.IsInbound.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(OmnichannelMessage.CustomerAddress)] = message.CustomerAddress;
            record.Fields[nameof(OmnichannelMessage.ServiceAddress)] = message.ServiceAddress;
            record.Fields[nameof(MessagingMessageDetails.Subject)] = subject;
            record.Fields[nameof(OmnichannelMessage.Content)] = message.Content;
            record.Fields[nameof(MessagingMessageDetails.QuotedText)] = message.GetQuotedText();
            record.Fields[nameof(EmailMessageMetadata.FromName)] = metadata?.FromName;
            record.Fields[nameof(EmailMessageMetadata.ReplyTo)] = metadata?.ReplyTo;
            record.Fields[nameof(EmailMessageMetadata.Cc)] = metadata?.Cc is { Count: > 0 } cc
                ? string.Join(", ", cc)
                : null;

            var attachments = message.GetAttachments();

            record.Fields[nameof(MessagingMessageAttachments)] = attachments.Count > 0
                ? string.Join(", ", attachments.Select(attachment => attachment.FileName ?? attachment.ContentType))
                : null;
        }

        foreach (var entry in await ListDeliveryLogAsync(addresses, cancellationToken))
        {
            var record = context.Add(DeliveryLogKind, entry.ItemId, entry.Kind.ToString(), entry.OccurredUtc);

            record.Fields[nameof(EmailDeliveryLogEntry.Kind)] = entry.Kind.ToString();
            record.Fields[nameof(EmailDeliveryLogEntry.Recipient)] = entry.Recipient;
            record.Fields[nameof(EmailDeliveryLogEntry.RecipientDomain)] = entry.RecipientDomain;
            record.Fields[nameof(EmailDeliveryLogEntry.MessageId)] = entry.MessageId;
            record.Fields[nameof(EmailDeliveryLogEntry.IsBulk)] = entry.IsBulk.ToString(CultureInfo.InvariantCulture);
            record.Fields[nameof(EmailDeliveryLogEntry.Detail)] = entry.Detail;
        }

        foreach (var suppression in await ListSuppressionsAsync(addresses, cancellationToken))
        {
            var record = context.Add(SuppressionKind, suppression.ItemId, suppression.Reason.ToString(), suppression.CreatedUtc);

            record.Fields[nameof(EmailSuppression.Address)] = suppression.Address;
            record.Fields[nameof(EmailSuppression.Reason)] = suppression.Reason.ToString();
            record.Fields[nameof(EmailSuppression.Detail)] = suppression.Detail;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var addresses = OmnichannelEmailAddress.WithCanonicalForms(context.Subject.Emails);

        if (addresses.Length == 0)
        {
            return;
        }

        foreach (var message in await ListMessagesAsync(addresses, cancellationToken))
        {
            foreach (var attachment in message.GetAttachments())
            {
                await _attachmentStore.DeleteAsync(attachment.Id, cancellationToken);
            }

            Erase(message);

            await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            context.Result.Count(MessageKind);
        }

        foreach (var entry in await ListDeliveryLogAsync(addresses, cancellationToken))
        {
            entry.Recipient = null;
            entry.Detail = null;

            await _session.SaveAsync(entry, collection: DeliverabilityCollection, cancellationToken: cancellationToken);
            context.Result.Count(DeliveryLogKind);
        }

        foreach (var suppression in await ListSuppressionsAsync(addresses, cancellationToken))
        {
            if (string.IsNullOrEmpty(suppression.Detail))
            {
                continue;
            }

            // The address stays, so the business keeps honouring the complaint or the bounce.
            suppression.Detail = null;

            await _session.SaveAsync(suppression, collection: DeliverabilityCollection, cancellationToken: cancellationToken);
            context.Result.Count(SuppressionKind);
        }
    }

    /// <summary>
    /// Clears what an email says and who it names, keeping the record and the identifiers it is threaded by.
    /// </summary>
    /// <param name="message">The email.</param>
    internal static void Erase(OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.Content = string.Empty;
        message.CustomerAddress = null;
        message.MediaReferences = [];
        message.SetSubject(null);
        message.SetQuotedText(null);
        message.SetAttachments([]);

        if (message.GetEmailMetadata() is { } metadata)
        {
            metadata.FromName = null;
            metadata.ReplyTo = null;
            metadata.Cc = [];

            message.Put(metadata);
        }
    }

    private async Task<IEnumerable<OmnichannelMessage>> ListMessagesAsync(string[] addresses, CancellationToken cancellationToken)
        => await _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == EmailChannelConstants.ChannelName && index.CustomerAddress.IsIn(addresses),
                collection: OmnichannelConstants.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .Take(MaxRecords)
            .ListAsync(cancellationToken);

    private async Task<IEnumerable<EmailDeliveryLogEntry>> ListDeliveryLogAsync(string[] addresses, CancellationToken cancellationToken)
        => await _session.Query<EmailDeliveryLogEntry, EmailDeliveryLogIndex>(
                index => index.Recipient.IsIn(addresses),
                collection: DeliverabilityCollection)
            .OrderBy(index => index.OccurredUtc)
            .Take(MaxRecords)
            .ListAsync(cancellationToken);

    private async Task<IEnumerable<EmailSuppression>> ListSuppressionsAsync(string[] addresses, CancellationToken cancellationToken)
        => await _session.Query<EmailSuppression, EmailSuppressionIndex>(
                index => index.Address.IsIn(addresses),
                collection: DeliverabilityCollection)
            .Take(MaxRecords)
            .ListAsync(cancellationToken);
}
