using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Removes the files of responses that no answer keeps: the files of a response that was deleted (or erased) without
/// them, the files of a request that failed after storing them (its session never commits), and the files an answer
/// dropped, such as the answer of a question hidden since. Only files older than a grace period are removed, so the
/// files of a request still being handled are left alone.
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
        DateTime? from = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (seen.Count < MaxPerRun && !cancellationToken.IsCancellationRequested)
        {
            // Pages start at the last page's upload time, which comes again, so the files already seen are skipped.
            var listed = await attachments.ListUploadedBeforeAsync(_kinds, before, from, PageSize, cancellationToken);
            var page = listed.Where(attachment => seen.Add(attachment.ItemId)).ToList();

            if (page.Count == 0)
            {
                break;
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
                break;
            }
        }

        return removed;
    }
}
