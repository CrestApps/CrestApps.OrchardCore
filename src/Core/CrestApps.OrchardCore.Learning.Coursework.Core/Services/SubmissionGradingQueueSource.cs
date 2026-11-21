using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Services;

/// <summary>
/// The coursework submissions that wait for a grader, for the grading list.
/// </summary>
internal sealed class SubmissionGradingQueueSource : IGradingQueueSource
{
    private readonly SubmissionStore _store;

    public SubmissionGradingQueueSource(SubmissionStore store)
    {
        _store = store;
    }

    public async Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (count, submissions) = await _store.PageWaitingAsync(query.CourseIds, 0, Math.Max(query.Take, 1), cancellationToken);

        return new GradingQueuePage
        {
            Count = count,
            Items = submissions.Select(ToItem).ToArray(),
        };
    }

    private static GradingQueueItem ToItem(Submission submission)
        => new()
        {
            Kind = CourseworkConstants.GradingQueueKind,
            Id = submission.ItemId,
            UserId = submission.UserId,
            CourseId = submission.CourseId,
            ItemId = submission.CourseworkId,
            ItemTitle = submission.CourseworkTitle,
            Reference = "#" + submission.Attempt.ToString(CultureInfo.InvariantCulture),
            Icon = "fa-solid fa-file-pen",
            SubmittedUtc = DateTime.SpecifyKind(submission.SubmittedUtc, DateTimeKind.Utc),
            Area = "CrestApps.OrchardCore.Learning.Coursework",
            Controller = "CourseworkGrading",
            Action = "Submission",
            RouteValues = new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = submission.ItemId },
        };
}
