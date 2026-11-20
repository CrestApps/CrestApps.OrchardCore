using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// One of the learner's own enrollments, as the my-progress tool returns it.
/// </summary>
public sealed class MyCourseProgress
{
    /// <summary>
    /// Gets or sets the course id.
    /// </summary>
    public string CourseId { get; set; }

    /// <summary>
    /// Gets or sets the course title.
    /// </summary>
    public string Course { get; set; }

    /// <summary>
    /// Gets or sets the enrollment's state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the share of required items completed.
    /// </summary>
    public int ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the number of required items completed.
    /// </summary>
    public int CompletedItems { get; set; }

    /// <summary>
    /// Gets or sets the number of required items.
    /// </summary>
    public int RequiredItems { get; set; }

    /// <summary>
    /// Gets or sets the local due date and time.
    /// </summary>
    public string Due { get; set; }

    /// <summary>
    /// Gets or sets the local time access ends.
    /// </summary>
    public string AccessEnds { get; set; }

    /// <summary>
    /// Gets or sets the local completion time.
    /// </summary>
    public string Completed { get; set; }

    /// <summary>
    /// Gets or sets the course grade in percent.
    /// </summary>
    public decimal? Grade { get; set; }

    /// <summary>
    /// Gets or sets whether the course was passed.
    /// </summary>
    public bool? Passed { get; set; }
}

/// <summary>
/// The next lesson a learner can open.
/// </summary>
public sealed class MyNextLesson
{
    /// <summary>
    /// Gets or sets the course title.
    /// </summary>
    public string Course { get; set; }

    /// <summary>
    /// Gets or sets the lesson title.
    /// </summary>
    public string Lesson { get; set; }

    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public string Section { get; set; }

    /// <summary>
    /// Gets or sets the course item kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the link that opens the lesson in the player.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// Reads the signed-in learner's own enrollments and their next lesson. It never reads anyone else's records.
/// </summary>
internal sealed class LearnerLearningReader
{
    private readonly IEnrollmentStore _enrollments;
    private readonly ILearnerProgressService _progress;
    private readonly ICourseStructureService _courseStructure;
    private readonly ICourseAccessService _courseAccess;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerLearningReader"/> class.
    /// </summary>
    public LearnerLearningReader(
        IEnrollmentStore enrollments,
        ILearnerProgressService progress,
        ICourseStructureService courseStructure,
        ICourseAccessService courseAccess,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock)
    {
        _enrollments = enrollments;
        _progress = progress;
        _courseStructure = courseStructure;
        _courseAccess = courseAccess;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
    }

    /// <summary>
    /// Gets the learner's enrollments with their progress, the open ones first, optionally of one course.
    /// </summary>
    /// <param name="user">The learner.</param>
    /// <param name="courseId">The course, or <see langword="null"/> for every course.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enrollments.</returns>
    public async Task<IReadOnlyList<MyCourseProgress>> GetProgressAsync(ClaimsPrincipal user, string courseId, CancellationToken cancellationToken)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var enrollments = (await _enrollments.ListByUserAsync(userId, cancellationToken))
            .Where(enrollment => string.IsNullOrEmpty(courseId) || string.Equals(enrollment.CourseId, courseId, StringComparison.Ordinal))
            .Where(enrollment => enrollment.Status != EnrollmentStatus.Withdrawn)
            .GroupBy(enrollment => enrollment.CourseId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(enrollment => enrollment.IsOpen).ThenByDescending(enrollment => enrollment.CreatedUtc).First())
            .OrderByDescending(enrollment => enrollment.IsOpen)
            .ThenByDescending(enrollment => enrollment.LastActivityUtc ?? enrollment.CreatedUtc)
            .ToList();
        var rows = new List<MyCourseProgress>();

        foreach (var enrollment in enrollments)
        {
            var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
            var required = 0;
            var completed = 0;

            if (course is not null)
            {
                var outline = await _courseStructure.GetOutlineAsync(course);
                var progress = await _progress.GetAsync(enrollment.ItemId, cancellationToken);

                foreach (var entry in outline.RequiredEntries)
                {
                    required++;

                    if (progress?.Find(entry.Item.ContentItemId)?.State == CourseItemState.Completed)
                    {
                        completed++;
                    }
                }
            }

            rows.Add(new MyCourseProgress
            {
                CourseId = enrollment.CourseId,
                Course = course?.DisplayText ?? enrollment.CourseId,
                Status = enrollment.Status.ToString(),
                ProgressPercent = enrollment.ProgressPercent,
                CompletedItems = completed,
                RequiredItems = required,
                Due = await LearningAIDates.FormatAsync(_localClock, enrollment.DueUtc),
                AccessEnds = await LearningAIDates.FormatAsync(_localClock, enrollment.AccessEndsUtc),
                Completed = await LearningAIDates.FormatAsync(_localClock, enrollment.CompletedUtc),
                Grade = enrollment.Grade,
                Passed = enrollment.Passed,
            });
        }

        return rows;
    }

    /// <summary>
    /// Gets the first item of an active course, in outline order, that the learner has not completed and may open now.
    /// </summary>
    /// <param name="user">The learner.</param>
    /// <param name="courseId">The course, or <see langword="null"/> for the course worked on last.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The lesson, or <see langword="null"/> when nothing is left to open now.</returns>
    public async Task<MyNextLesson> GetNextLessonAsync(ClaimsPrincipal user, string courseId, CancellationToken cancellationToken)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var enrollments = (await _enrollments.ListByUserAsync(userId, cancellationToken))
            .Where(enrollment => enrollment.Status == EnrollmentStatus.Active)
            .Where(enrollment => string.IsNullOrEmpty(courseId) || string.Equals(enrollment.CourseId, courseId, StringComparison.Ordinal))
            .OrderByDescending(enrollment => enrollment.LastActivityUtc ?? enrollment.CreatedUtc)
            .ToList();

        foreach (var enrollment in enrollments)
        {
            var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);

            if (course is null)
            {
                continue;
            }

            var outline = await _courseStructure.GetOutlineAsync(course);
            var progress = await _progress.GetAsync(enrollment.ItemId, cancellationToken);

            foreach (var entry in outline.Entries.Where(entry => !entry.Inline))
            {
                if (progress?.Find(entry.Item.ContentItemId)?.State == CourseItemState.Completed)
                {
                    continue;
                }

                var access = await _courseAccess.GetItemAccessAsync(user, entry.Item);

                if (!access.Allowed)
                {
                    continue;
                }

                var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

                return new MyNextLesson
                {
                    Course = course.DisplayText,
                    Lesson = entry.Item.DisplayText,
                    Section = entry.Section?.Title,
                    Kind = entry.Part?.Kind,
                    Url = $"{pathBase}/learning/courses/{Uri.EscapeDataString(course.ContentItemId)}/play/{Uri.EscapeDataString(entry.Item.ContentItemId)}",
                };
            }
        }

        return null;
    }
}
