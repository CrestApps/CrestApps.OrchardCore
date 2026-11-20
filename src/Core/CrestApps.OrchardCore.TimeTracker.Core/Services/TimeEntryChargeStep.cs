using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.TimeTracker.Core.Services;

/// <summary>
/// Asks the charge resolvers what the entries logged before entries remembered it count against, a page at a time, so
/// time already logged keeps the record it counts against from now on (such as the agreement its ticket works under
/// today). It picks up where it stopped on its next run.
/// </summary>
public sealed class TimeEntryChargeStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "TimeTracker.ChargeEntries";

    private const int EntriesPerRun = 200;
    private const string CursorKey = "after";

    private readonly ISession _session;
    private readonly TimeEntryStore _entries;
    private readonly IEnumerable<ITimeEntryChargeResolver> _resolvers;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeEntryChargeStep"/> class.
    /// </summary>
    public TimeEntryChargeStep(ISession session, TimeEntryStore entries, IEnumerable<ITimeEntryChargeResolver> resolvers)
    {
        _session = session;
        _entries = entries;
        _resolvers = resolvers;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_resolvers.Any())
        {
            return true;
        }

        // An entry that is charged leaves the query; one that stays uncharged is passed once, by the cursor.
        var afterId = context.Data.TryGetValue(CursorKey, out var cursor) && long.TryParse(cursor, out var parsed) ? parsed : 0;
        var rows = (await _session.QueryIndex<TimeEntryIndex>(
                index => index.Id > afterId && index.ChargedTo == null && index.SourceRef != null,
                collection: TimeTrackerConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(EntriesPerRun)
            .ListAsync(cancellationToken)).ToList();
        var rowIds = rows.Select(row => row.Id).ToArray();
        var entries = rows.Count == 0
            ? []
            : await _session.Query<TimeEntry, TimeEntryIndex>(index => index.Id.IsIn(rowIds), collection: TimeTrackerConstants.CollectionName).ListAsync(cancellationToken);

        foreach (var entry in entries)
        {
            foreach (var resolver in _resolvers)
            {
                var chargedTo = await resolver.ResolveAsync(entry, cancellationToken);

                if (!chargedTo.IsEmpty)
                {
                    entry.ChargedTo = chargedTo;
                    await _entries.UpdateAsync(entry, cancellationToken);

                    break;
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
