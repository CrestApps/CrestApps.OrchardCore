using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// A requester message: the ticket it is about, its kind, the requester and the message as Tickets wrote it, which
/// <see cref="Services.TicketEmailOutbox"/> sends as a notification or through the templated e-mail outbox. Earlier
/// versions kept these in the service desk's collection as an outbox of their own; the ones still stored are handed to
/// the templated e-mail outbox (<see cref="Services.TicketEmailHandOverStep"/>) and removed.
/// </summary>
public sealed class TicketEmailOutboxItem
{
    public long Id { get; set; }

    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the timeline entry the e-mail carries (a reply), if any; its delivery is shown on it.
    /// </summary>
    public string EntryId { get; set; }

    public string Kind { get; set; }

    public string To { get; set; }

    /// <summary>
    /// Gets or sets the requester's user id when they have a site account: the message then goes through the site's
    /// notifications (<see cref="Services.ITicketRequesterNotifier"/>), and by e-mail to <see cref="To"/> only when the
    /// account is gone or the Notifications feature is off.
    /// </summary>
    public string RecipientUserId { get; set; }

    public string Reference { get; set; }

    public string Subject { get; set; }

    public string Html { get; set; }

    public string TicketNumber { get; set; }

    public string TicketSubject { get; set; }

    public string TicketCategory { get; set; }

    public string TicketStatus { get; set; }

    public string RequesterName { get; set; }

    public string TicketLink { get; set; }

    public string ReplyBody { get; set; }

    public string ReplyHtml { get; set; }

    public string ReplyAuthorName { get; set; }

    public TicketEmailOutboxStatus Status { get; set; }

    public int Attempts { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? NextAttemptUtc { get; set; }

    public DateTime? SentUtc { get; set; }

    public string LastError { get; set; }
}

/// <summary>
/// Where an outbox e-mail stands.
/// </summary>
public enum TicketEmailOutboxStatus
{
    Pending,
    Sent,
    Abandoned,
}

/// <summary>
/// Finds the requester e-mails earlier versions kept, by ticket, so a requester who asks to be forgotten loses them too.
/// </summary>
public sealed class TicketEmailOutboxIndex : MapIndex
{
    public string TicketId { get; set; }

    public TicketEmailOutboxStatus Status { get; set; }

    public DateTime? NextAttemptUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TicketEmailOutboxItem"/> to <see cref="TicketEmailOutboxIndex"/>.
/// </summary>
public sealed class TicketEmailOutboxIndexProvider : IndexProvider<TicketEmailOutboxItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailOutboxIndexProvider"/> class, which maps to the service desk's collection.
    /// </summary>
    public TicketEmailOutboxIndexProvider()
    {
        CollectionName = TicketsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TicketEmailOutboxItem> context)
        => context.For<TicketEmailOutboxIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(item => new TicketEmailOutboxIndex
            {
                TicketId = item.TicketId,
                Status = item.Status,
                NextAttemptUtc = item.NextAttemptUtc,
            });
}
