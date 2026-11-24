using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Saves the timeline entries made before the file index existed again, a page at a time, so their files are indexed. It
/// picks up where it stopped on its next run.
/// </summary>
public sealed class TimelineFileIndexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Timeline.IndexFiles";

    private const int EntriesPerRun = 200;
    private const string CursorKey = "after";

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineFileIndexStep"/> class.
    /// </summary>
    public TimelineFileIndexStep(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<TimelineEntryIndex>(index => index.Id > afterId, collection: TimelineConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(EntriesPerRun)
            .ListAsync(cancellationToken)).ToList();

        if (rows.Count > 0)
        {
            var rowIds = rows.Select(row => row.Id).ToArray();

            foreach (var entry in await _session.Query<TimelineEntry, TimelineEntryIndex>(index => index.Id.IsIn(rowIds), collection: TimelineConstants.CollectionName).ListAsync(cancellationToken))
            {
                if (entry.AttachmentIds is { Count: > 0 })
                {
                    await _session.SaveAsync(entry, false, TimelineConstants.CollectionName, cancellationToken);
                }
            }
        }

        if (rows.Count < EntriesPerRun)
        {
            return true;
        }

        context.Data[CursorKey] = rows[^1].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return false;
    }
}
