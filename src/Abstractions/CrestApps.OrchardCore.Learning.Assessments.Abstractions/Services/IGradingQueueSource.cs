namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Gives the grading list the work of one kind that waits for a grader, such as quiz attempts or coursework
/// submissions. The grading list asks every source for at most the items it needs, merges them by submitted time,
/// oldest first, and pages across the merged list.
/// </summary>
public interface IGradingQueueSource
{
    /// <summary>
    /// Lists the oldest work that waits for a grader, oldest first, with the count of all of it.
    /// </summary>
    /// <param name="query">The courses and the most items to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the items.</returns>
    Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the grading list asks a source for.
/// </summary>
public sealed class GradingQueueQuery
{
    /// <summary>
    /// Gets or sets the courses the grader grades, or <see langword="null"/> for every course.
    /// </summary>
    public IReadOnlyCollection<string> CourseIds { get; set; }

    /// <summary>
    /// Gets or sets the user id of the grader the list is for, or <see langword="null"/>. Nobody grades their own work,
    /// so a source leaves out the grader's own work.
    /// </summary>
    public string GraderUserId { get; set; }

    /// <summary>
    /// Gets or sets the most items to return: the items before the page and the page itself.
    /// </summary>
    public int Take { get; set; }
}

/// <summary>
/// What a source returns.
/// </summary>
public sealed class GradingQueuePage
{
    /// <summary>
    /// Gets or sets how much work of the source waits in all.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the oldest items, oldest first.
    /// </summary>
    public IReadOnlyList<GradingQueueItem> Items { get; set; } = [];
}

/// <summary>
/// One piece of work that waits for a grader.
/// </summary>
public sealed class GradingQueueItem
{
    /// <summary>
    /// Gets or sets the kind of work, such as Attempt or Submission.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the learner, or <see langword="null"/> when the work must not name them, such as a response to an
    /// anonymous survey.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course item the work is for.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the title of the course item.
    /// </summary>
    public string ItemTitle { get; set; }

    /// <summary>
    /// Gets or sets a short reference shown under the learner, such as the attempt number.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome classes of the kind's icon.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets when the work was handed in, in UTC.
    /// </summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>
    /// Gets or sets the day the work was handed in, already formatted, when only its day may be shown, such as a response
    /// to an anonymous survey, whose day is read in the site's time zone; otherwise <see langword="null"/>. A page shows
    /// it as it is, so the day is never converted again in the viewer's time zone.
    /// </summary>
    public string SubmittedDay { get; set; }

    /// <summary>
    /// Gets or sets the area of the page that grades the work.
    /// </summary>
    public string Area { get; set; }

    /// <summary>
    /// Gets or sets the controller of the page that grades the work.
    /// </summary>
    public string Controller { get; set; }

    /// <summary>
    /// Gets or sets the action of the page that grades the work.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Gets or sets the route values of the page that grades the work, besides the area.
    /// </summary>
    public IDictionary<string, object> RouteValues { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
