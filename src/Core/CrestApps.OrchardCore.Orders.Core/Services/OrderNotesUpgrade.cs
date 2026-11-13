using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Moves the notes stored on orders to their timelines: each note becomes an internal entry (a note, or an event for
/// what happened) with its own time and author name, and the order no longer carries it. Running it again does nothing
/// more.
/// </summary>
public sealed class OrderNotesUpgrade
{
    private const int BatchSize = 100;

    private readonly ISession _session;
    private readonly ITimelineService _timeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderNotesUpgrade"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="timeline">The timeline service.</param>
    public OrderNotesUpgrade(ISession session, ITimelineService timeline)
    {
        _session = session;
        _timeline = timeline;
    }

    /// <summary>
    /// Moves the notes of every order.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many notes were moved.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var moved = 0;
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = (await _session.Query<Order, OrderIndex>(collection: OrderConstants.CollectionName)
                .OrderBy(index => index.ItemId)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            foreach (var order in batch)
            {
#pragma warning disable CS0618 // The stored notes are read once here, to move them.
                var notes = order.Notes ?? [];

                if (notes.Count == 0)
                {
                    continue;
                }

                foreach (var note in notes.Where(note => !string.IsNullOrWhiteSpace(note.Text)))
                {
                    await _timeline.AddAsync(new TimelineEntry
                    {
                        Subject = new RecordRef(OrderConstants.RecordKind, order.ItemId),
                        Kind = note.Kind == OrderNoteKind.Event ? TimelineConstants.Kinds.Event : TimelineConstants.Kinds.Note,
                        Visibility = TimelineVisibility.Internal,
                        AuthorName = note.AuthorName,
                        Body = note.Text,
                        CreatedUtc = DateTime.SpecifyKind(note.CreatedUtc, DateTimeKind.Utc),
                    }, cancellationToken);

                    moved++;
                }

                order.Notes = [];
#pragma warning restore CS0618
                await _session.SaveAsync(order, collection: OrderConstants.CollectionName, cancellationToken: cancellationToken);
            }

            if (batch.Count < BatchSize)
            {
                break;
            }

            page++;
        }

        return moved;
    }
}
