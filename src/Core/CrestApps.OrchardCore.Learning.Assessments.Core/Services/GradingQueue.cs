using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// The grading list: the work of every <see cref="IGradingQueueSource"/> that waits for a grader, merged by submitted
/// time, oldest first, and paged across the merged list. Each source is asked for no more than the items before the
/// page and the page itself.
/// </summary>
public sealed class GradingQueue
{
    private readonly IEnumerable<IGradingQueueSource> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradingQueue"/> class.
    /// </summary>
    /// <param name="sources">The sources.</param>
    public GradingQueue(IEnumerable<IGradingQueueSource> sources)
    {
        _sources = sources;
    }

    /// <summary>
    /// Reads a page of the merged list.
    /// </summary>
    /// <param name="courseIds">The courses the grader grades, or <see langword="null"/> for every course.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count of all waiting work and the page.</returns>
    public async Task<GradingQueuePage> PageAsync(IReadOnlyCollection<string> courseIds, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Max(pageSize, 1);

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var query = new GradingQueueQuery { CourseIds = courseIds, Take = skip + pageSize };
        var count = 0;
        var items = new List<GradingQueueItem>();

        foreach (var source in _sources)
        {
            var result = await source.ListWaitingAsync(query, cancellationToken);

            if (result is null)
            {
                continue;
            }

            count += result.Count;
            items.AddRange((result.Items ?? []).Take(query.Take));
        }

        return new GradingQueuePage
        {
            Count = count,
            Items = items
                .OrderBy(item => item.SubmittedUtc)
                .ThenBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .Skip(skip)
                .Take(pageSize)
                .ToArray(),
        };
    }
}

/// <summary>
/// The quiz attempts that wait for a grader.
/// </summary>
internal sealed class AttemptGradingQueueSource : IGradingQueueSource
{
    private readonly AssessmentAttemptStore _attemptStore;

    public AttemptGradingQueueSource(AssessmentAttemptStore attemptStore)
    {
        _attemptStore = attemptStore;
    }

    public async Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (count, attempts) = await _attemptStore.PageNeedingGradingAsync(query.CourseIds, 1, Math.Max(query.Take, 1), cancellationToken);

        return new GradingQueuePage
        {
            Count = count,
            Items = attempts.Select(ToItem).ToArray(),
        };
    }

    private static GradingQueueItem ToItem(AssessmentAttempt attempt)
        => new()
        {
            Kind = AssessmentsConstants.AttemptGradingQueueKind,
            Id = attempt.ItemId,
            UserId = attempt.UserId,
            CourseId = attempt.CourseId,
            ItemId = attempt.QuizId,
            ItemTitle = attempt.QuizTitle,
            Reference = attempt.Number,
            Icon = "fa-solid fa-circle-question",
            SubmittedUtc = DateTime.SpecifyKind(attempt.SubmittedUtc ?? attempt.StartedUtc, DateTimeKind.Utc),
            Area = "CrestApps.OrchardCore.Learning.Assessments",
            Controller = "Grading",
            Action = "Attempt",
            RouteValues = new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = attempt.ItemId },
        };
}
