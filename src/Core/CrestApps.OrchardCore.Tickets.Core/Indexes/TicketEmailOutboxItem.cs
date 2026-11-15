using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// An e-mail to a requester waiting to be sent, sent, or given up on, kept so a send that fails is tried again and the
/// ticket shows whether the requester got it.
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

    public string Reference { get; set; }

    public string Subject { get; set; }

    public string Html { get; set; }

    public string TicketNumber { get; set; }

    public string TicketSubject { get; set; }

    public string TicketStatus { get; set; }

    public string RequesterName { get; set; }

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
/// Finds the outbox e-mails due to be tried.
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
    /// <inheritdoc/>
    public override void Describe(DescribeContext<TicketEmailOutboxItem> context)
        => context.For<TicketEmailOutboxIndex>()
            .Map(item => new TicketEmailOutboxIndex
            {
                TicketId = item.TicketId,
                Status = item.Status,
                NextAttemptUtc = item.NextAttemptUtc,
            });
}
