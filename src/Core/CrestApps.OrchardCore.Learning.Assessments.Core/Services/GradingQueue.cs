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
    public Task<GradingQueuePage> PageAsync(IReadOnlyCollection<string> courseIds, int page, int pageSize, CancellationToken cancellationToken = default)
        => PageAsync(courseIds, null, page, pageSize, cancellationToken);

    /// <summary>
    /// Reads a page of the merged list for a grader: their own work is left out, since nobody grades their own work.
    /// </summary>
    /// <param name="courseIds">The courses the grader grades, or <see langword="null"/> for every course.</param>
    /// <param name="graderUserId">The grader's user id, or <see langword="null"/>.</param>
    /// <param name="page">The page, from one.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count of all waiting work and the page.</returns>
    public async Task<GradingQueuePage> PageAsync(IReadOnlyCollection<string> courseIds, string graderUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Max(pageSize, 1);

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var query = new GradingQueueQuery { CourseIds = courseIds, GraderUserId = graderUserId, Take = skip + pageSize };
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
