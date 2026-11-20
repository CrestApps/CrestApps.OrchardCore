using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// That a time-elapsed workflow start has fired for a ticket and a moment, so it fires once per ticket and moment. One
/// small record per start and ticket; it goes once the ticket is no longer being worked on.
/// </summary>
public sealed class TicketTimeElapsedMark
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the start, as <c>workflow type:activity</c>.
    /// </summary>
    public string StartKey { get; set; }

    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the moment of the ticket the start fired for, such as its creation or last reply.
    /// </summary>
    public DateTime Moment { get; set; }

    /// <summary>
    /// Gets or sets when it fired.
    /// </summary>
    public DateTime FiredUtc { get; set; }
}

/// <summary>
/// Finds the markers of a start and of tickets.
/// </summary>
public sealed class TicketTimeElapsedMarkIndex : MapIndex
{
    public string StartKey { get; set; }

    public string TicketId { get; set; }

    public DateTime Moment { get; set; }

    public DateTime FiredUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TicketTimeElapsedMark"/> to <see cref="TicketTimeElapsedMarkIndex"/>.
/// </summary>
public sealed class TicketTimeElapsedMarkIndexProvider : IndexProvider<TicketTimeElapsedMark>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTimeElapsedMarkIndexProvider"/> class, which maps to the service desk's collection.
    /// </summary>
    public TicketTimeElapsedMarkIndexProvider()
    {
        CollectionName = TicketsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<TicketTimeElapsedMark> context)
        => context.For<TicketTimeElapsedMarkIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(mark => new TicketTimeElapsedMarkIndex
            {
                StartKey = mark.StartKey,
                TicketId = mark.TicketId,
                Moment = mark.Moment,
                FiredUtc = mark.FiredUtc,
            });
}
