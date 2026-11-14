using CrestApps.OrchardCore.Tickets.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// Finds the backlog snapshots of a day and desk.
/// </summary>
public sealed class TicketBacklogIndex : MapIndex
{
    public string Day { get; set; }

    public string DeskId { get; set; }

    public DateTime TakenUtc { get; set; }
}

/// <summary>
/// Maps <see cref="TicketBacklogDay"/> to <see cref="TicketBacklogIndex"/>.
/// </summary>
public sealed class TicketBacklogIndexProvider : IndexProvider<TicketBacklogDay>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<TicketBacklogDay> context)
        => context.For<TicketBacklogIndex>()
            .Map(day => new TicketBacklogIndex
            {
                Day = day.Day,
                DeskId = day.DeskId,
                TakenUtc = day.TakenUtc,
            });
}
