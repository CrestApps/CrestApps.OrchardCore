using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Moves the notes stored on work orders to their timelines: each note becomes an internal note entry with its own
/// time and author name, and the work order no longer carries it. Running it again does nothing more.
/// </summary>
public sealed class WorkOrderNotesUpgrade
{
    private const int BatchSize = 100;

    private readonly ISession _session;
    private readonly ITimelineService _timeline;
    private readonly WorkOrderStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderNotesUpgrade"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="timeline">The timeline service.</param>
    /// <param name="store">The work order store.</param>
    public WorkOrderNotesUpgrade(ISession session, ITimelineService timeline, WorkOrderStore store)
    {
        _session = session;
        _timeline = timeline;
        _store = store;
    }

    /// <summary>
    /// Moves the notes of every work order.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many notes were moved.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var moved = 0;
        var page = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = (await _session.Query<WorkOrder, WorkOrderIndex>(collection: WorkOrdersConstants.CollectionName)
                .OrderBy(index => index.ItemId)
                .Skip(page * BatchSize)
                .Take(BatchSize)
                .ListAsync(cancellationToken)).ToList();

            foreach (var workOrder in batch)
            {
#pragma warning disable CS0618 // The stored notes are read once here, to move them.
                var notes = workOrder.Notes ?? [];

                if (notes.Count == 0)
                {
                    continue;
                }

                foreach (var note in notes.Where(note => !string.IsNullOrWhiteSpace(note.Text)))
                {
                    await _timeline.AddAsync(new TimelineEntry
                    {
                        Subject = WorkOrderServiceLevels.SubjectOf(workOrder),
                        Kind = TimelineConstants.Kinds.Note,
                        Visibility = TimelineVisibility.Internal,
                        AuthorName = note.By,
                        Body = note.Text,
                        CreatedUtc = DateTime.SpecifyKind(note.Utc, DateTimeKind.Utc),
                    }, cancellationToken);

                    moved++;
                }

                workOrder.Notes = [];
#pragma warning restore CS0618
                await _store.UpdateAsync(workOrder, cancellationToken);
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
