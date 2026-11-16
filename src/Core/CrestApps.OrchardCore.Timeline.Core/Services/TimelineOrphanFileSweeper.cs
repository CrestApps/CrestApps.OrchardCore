using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Timeline.Core.Services;

/// <summary>
/// Removes the files of records with a timeline that no timeline entry keeps: a file is stored before the entry that
/// holds it, so when the request fails after the file was stored (its session never commits), the file stays behind.
/// Only files older than a grace period are removed, so the files of a request still being handled are left alone, and
/// nothing is removed while the entries made before the file index existed are still being indexed
/// (<see cref="TimelineFileIndexStep"/>): their files are not in the index yet.
/// </summary>
public sealed class TimelineOrphanFileSweeper
{
    /// <summary>
    /// How long a file may go without an entry before it is removed.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    private const int PageSize = 200;
    private const int MaxPerRun = 2000;

    private readonly ISession _session;
    private readonly IEnumerable<ITimelineAccessProvider> _providers;
    private readonly IEnumerable<IAttachmentService> _attachments;
    private readonly ResumableSteps _steps;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineOrphanFileSweeper"/> class.
    /// </summary>
    public TimelineOrphanFileSweeper(
        ISession session,
        IEnumerable<ITimelineAccessProvider> providers,
        IEnumerable<IAttachmentService> attachments,
        ResumableSteps steps,
        IClock clock,
        ILogger<TimelineOrphanFileSweeper> logger)
    {
        _session = session;
        _providers = providers;
        _attachments = attachments;
        _steps = steps;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Removes the orphan files, a page at a time, up to a limit per run.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many files were removed.</returns>
    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        // Files belong to entries on the records of the kinds that have a timeline.
        var kinds = _providers.Select(provider => provider.SubjectKind).Where(kind => !string.IsNullOrEmpty(kind)).Distinct(StringComparer.Ordinal).ToArray();

        if (kinds.Length == 0 || _attachments.FirstOrDefault() is not { } attachments)
        {
            return 0;
        }

        // Until the older entries are indexed, their files look like files no entry kept.
        if (await _steps.IsPendingAsync(TimelineFileIndexStep.StepName))
        {
            return 0;
        }

        var before = _clock.UtcNow - GracePeriod;
        var removed = 0;
        DateTime? from = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (seen.Count < MaxPerRun && !cancellationToken.IsCancellationRequested)
        {
            // Pages start at the last page's upload time, which comes again, so the files already seen are skipped.
            var listed = await attachments.ListUploadedBeforeAsync(kinds, before, from, PageSize, cancellationToken);
            var page = listed.Where(attachment => seen.Add(attachment.ItemId)).ToList();

            if (page.Count == 0)
            {
                break;
            }

            from = listed[^1].UploadedUtc;
            var ids = page.Select(attachment => attachment.ItemId).ToArray();
            var kept = (await _session.QueryIndex<TimelineAttachmentIndex>(index => index.AttachmentId.IsIn(ids), collection: TimelineConstants.CollectionName)
                    .ListAsync(cancellationToken))
                .Select(row => row.AttachmentId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var attachment in page.Where(attachment => !kept.Contains(attachment.ItemId)))
            {
                if (await attachments.DeleteAsync(attachment.ItemId, cancellationToken))
                {
                    removed++;

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Removed the file {AttachmentId} of {Owner}, which no timeline entry kept.", attachment.ItemId, attachment.Owner.ToString());
                    }
                }
            }

            if (listed.Count < PageSize)
            {
                break;
            }
        }

        return removed;
    }
}
