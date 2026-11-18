using System.Globalization;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Logging;
using YesSql;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Moves the notes stored on work orders to their timelines: each note becomes an internal note entry with its own
/// time and author name, and the work order no longer carries it. The notes are brought in through the timeline's import
/// path, so nothing is announced and no note is cut or refused for its length; a work order keeps its notes until every
/// one of them is on its timeline. Entry ids come from the work order and the note, so running it again adds nothing.
/// </summary>
internal sealed class WorkOrderNotesUpgrade
{
    private readonly ISession _session;
    private readonly ITimelineService _timeline;
    private readonly WorkOrderStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderNotesUpgrade"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="timeline">The timeline service.</param>
    /// <param name="store">The work order store.</param>
    /// <param name="logger">The logger.</param>
    public WorkOrderNotesUpgrade(ISession session, ITimelineService timeline, WorkOrderStore store, ILogger<WorkOrderNotesUpgrade> logger)
    {
        _session = session;
        _timeline = timeline;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets how many work orders are handled, and committed, at a time.
    /// </summary>
    internal int BatchSize { get; set; } = 100;

    /// <summary>
    /// Moves the notes of every work order, a batch at a time, committing each batch.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many notes were moved.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var moved = 0;
        var afterDocumentId = 0L;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await RunBatchesAsync(afterDocumentId, maxBatches: 1, cancellationToken);

            moved += batch.Count;
            afterDocumentId = batch.LastDocumentId;

            if (batch.Done)
            {
                break;
            }
        }

        return moved;
    }

    /// <summary>
    /// Handles up to a number of batches of work orders whose notes move, from where an earlier run got to, committing each batch.
    /// </summary>
    /// <param name="afterDocumentId">The last document id an earlier run handled, or 0 to start.</param>
    /// <param name="maxBatches">The most batches to handle.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were handled, the last document id handled, and whether there is nothing left.</returns>
    internal async Task<(int Count, long LastDocumentId, bool Done)> RunBatchesAsync(long afterDocumentId, int maxBatches, CancellationToken cancellationToken = default)
    {
        var moved = 0;

        for (var round = 0; round < maxBatches && !cancellationToken.IsCancellationRequested; round++)
        {
            // Paged by document id, which only grows, so a work order created meanwhile never shifts a page.
            var rows = await _session.QueryIndex<WorkOrderIndex>(index => index.DocumentId > afterDocumentId, collection: WorkOrdersConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(BatchSize)
                .ListAsync(cancellationToken);
            var documentIds = rows.Select(row => row.DocumentId).ToArray();

            if (documentIds.Length == 0)
            {
                return (moved, afterDocumentId, true);
            }

            afterDocumentId = documentIds[^1];

            foreach (var workOrder in await _session.GetAsync<WorkOrder>(documentIds, WorkOrdersConstants.CollectionName, cancellationToken))
            {
                if (workOrder is not null)
                {
                    moved += await MoveAsync(workOrder, cancellationToken);
                }
            }

            await _session.SaveChangesAsync(cancellationToken);

            if (documentIds.Length < BatchSize)
            {
                return (moved, afterDocumentId, true);
            }
        }

        return (moved, afterDocumentId, false);
    }

    private async Task<int> MoveAsync(WorkOrder workOrder, CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // The stored notes are read once here, to move them.
        var notes = workOrder.Notes ?? [];

        if (notes.Count == 0)
        {
            return 0;
        }

        var subject = WorkOrderServiceLevels.SubjectOf(workOrder);
        var moved = 0;
        var refused = 0;

        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index];

            if (string.IsNullOrWhiteSpace(note?.Text))
            {
                continue;
            }

            var result = await _timeline.ImportAsync(new TimelineEntry
            {
                ItemId = TimelineEntryIds.FromSource(subject.ToString(), "note", index.ToString(CultureInfo.InvariantCulture), note.Utc.Ticks.ToString(CultureInfo.InvariantCulture)),
                Subject = subject,
                Kind = TimelineConstants.Kinds.Note,
                Visibility = TimelineVisibility.Internal,
                AuthorName = note.By,
                Body = note.Text,
                CreatedUtc = DateTime.SpecifyKind(note.Utc, DateTimeKind.Utc),
            }, cancellationToken);

            if (result.Succeeded)
            {
                moved++;
            }
            else
            {
                refused++;
            }
        }

        // A work order whose notes didn't all reach the timeline keeps them, for the next run.
        if (refused > 0)
        {
            _logger.LogWarning("{Count} notes of a work order couldn't be moved to its timeline; the work order keeps them.", refused);

            return moved;
        }

        workOrder.Notes = [];
#pragma warning restore CS0618
        await _store.UpdateAsync(workOrder, cancellationToken);

        return moved;
    }

    // Reads the work order index table with the id of each row's document, which the index model doesn't expose, so the
    // work orders can be paged by it. Its name matches the table; nothing writes through it.
    private sealed class WorkOrderIndex : MapIndex
    {
        public long DocumentId { get; set; }
    }
}
