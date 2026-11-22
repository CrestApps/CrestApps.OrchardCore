using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Removes the files of responses that no answer keeps: the files of a response that was deleted (or erased) without
/// them, and the files an answer dropped, such as the answer of a question hidden since. (A request that fails after
/// storing files removes them itself: their records roll back with it, so no sweep could find them.) Only files older
/// than a grace period are removed, so the files of a request still being handled are left alone. Each run goes on from
/// where the last one stopped (<see cref="SurveyOrphanFileSweepCursor"/>) and starts over from the oldest file once it
/// reaches the newest, so a site that keeps more files than one run looks at still has every file checked in turn.
/// </summary>
public sealed class SurveyResponseOrphanFileSweeper
{
    /// <summary>
    /// How long a file may go without an answer before it is removed.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);

    private const int PageSize = 200;
    private const int MaxPerRun = 2000;

    private static readonly string[] _kinds = [SurveysConstants.ResponseRecordKind];

    private readonly SurveyResponseStore _responses;
    private readonly IEnumerable<IAttachmentService> _attachments;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseOrphanFileSweeper"/> class.
    /// </summary>
    /// <param name="responses">The response store.</param>
    /// <param name="attachments">The attachment service, when Attachments is on.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SurveyResponseOrphanFileSweeper(
        SurveyResponseStore responses,
        IEnumerable<IAttachmentService> attachments,
        IClock clock,
        ILogger<SurveyResponseOrphanFileSweeper> logger)
    {
        _responses = responses;
        _attachments = attachments;
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
        if (_attachments.FirstOrDefault() is not { } attachments)
        {
            return 0;
        }

        var before = _clock.UtcNow - GracePeriod;
        var removed = 0;
        var session = _responses.YesSqlSession;
        var cursor = await session.Query<SurveyOrphanFileSweepCursor>(SurveysConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new SurveyOrphanFileSweepCursor();
        DateTime? from = cursor.NextFromUtc is { } next && next < before ? DateTime.SpecifyKind(next, DateTimeKind.Utc) : null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reachedEnd = false;

        while (seen.Count < MaxPerRun && !cancellationToken.IsCancellationRequested)
        {
            // Pages start at the last page's upload time, which comes again, so the files already seen are skipped.
            var listed = await attachments.ListUploadedBeforeAsync(_kinds, before, from, PageSize, cancellationToken);
            var page = listed.Where(attachment => seen.Add(attachment.ItemId)).ToList();

            if (page.Count == 0)
            {
                if (listed.Count < PageSize)
                {
                    reachedEnd = true;

                    break;
                }

                // A whole page of files uploaded at the same moment, all seen: go on past that moment.
                from = listed[^1].UploadedUtc.AddTicks(1);

                continue;
            }

            from = listed[^1].UploadedUtc;

            var responseIds = page.Select(attachment => attachment.Owner.Id).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
            var kept = (await _responses.GetAsync(responseIds, cancellationToken))
                .SelectMany(SurveyResponseFiles.ReferencedIds)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var attachment in page.Where(attachment => !kept.Contains(attachment.ItemId)))
            {
                if (await attachments.DeleteAsync(attachment.ItemId, cancellationToken))
                {
                    removed++;

                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Removed the file {AttachmentId} of {Owner}, which no survey answer kept.", attachment.ItemId, attachment.Owner.ToString());
                    }
                }
            }

            if (listed.Count < PageSize)
            {
                reachedEnd = true;

                break;
            }
        }

        // The next run goes on from here, or starts over from the oldest file once this one reached the newest.
        cursor.NextFromUtc = reachedEnd ? null : from;
        await session.SaveAsync(cursor, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        return removed;
    }
}
