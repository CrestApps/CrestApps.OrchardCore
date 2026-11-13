using System.Globalization;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// Turns enrollments into the text the Learning coded reports show: course titles, user names and times in the
/// viewer's time zone.
/// </summary>
internal sealed class LearningReportText
{
    /// <summary>
    /// The most rows a Learning coded report reads.
    /// </summary>
    public const int MaxRows = 5000;

    private readonly ICourseStructureService _courseStructure;
    private readonly LearnerUserLookup _userLookup;
    private readonly ILocalClock _localClock;
    private readonly Dictionary<string, string> _courseTitles = new(StringComparer.Ordinal);

    private IDictionary<string, string> _userNames = new Dictionary<string, string>(StringComparer.Ordinal);

    public LearningReportText(
        ICourseStructureService courseStructure,
        LearnerUserLookup userLookup,
        ILocalClock localClock)
    {
        _courseStructure = courseStructure;
        _userLookup = userLookup;
        _localClock = localClock;
    }

    /// <summary>
    /// Reads the user names and course titles of enrollments.
    /// </summary>
    /// <param name="enrollments">The enrollments.</param>
    /// <returns>A task.</returns>
    public async Task LoadAsync(IEnumerable<Enrollment> enrollments)
    {
        var list = enrollments as IReadOnlyCollection<Enrollment> ?? enrollments.ToArray();

        _userNames = await _userLookup.GetUserNamesAsync(list.Select(enrollment => enrollment.UserId));

        foreach (var courseId in list.Select(enrollment => enrollment.CourseId).Distinct(StringComparer.Ordinal))
        {
            if (!_courseTitles.ContainsKey(courseId))
            {
                _courseTitles[courseId] = (await _courseStructure.GetCourseAsync(courseId))?.DisplayText;
            }
        }
    }

    /// <summary>
    /// Gets the user name of a learner, or the user id when the user is gone.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <returns>The name.</returns>
    public string Learner(Enrollment enrollment)
        => _userNames.TryGetValue(enrollment.UserId ?? string.Empty, out var name) ? name : enrollment.UserId;

    /// <summary>
    /// Gets the title of a course, or the title kept on the completion, or the id.
    /// </summary>
    /// <param name="enrollment">The enrollment.</param>
    /// <returns>The title.</returns>
    public string Course(Enrollment enrollment)
        => _courseTitles.TryGetValue(enrollment.CourseId ?? string.Empty, out var title) && !string.IsNullOrEmpty(title)
            ? title
            : enrollment.Completion?.CourseTitle ?? enrollment.CourseId;

    /// <summary>
    /// Formats a UTC time in the viewer's time zone.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The text, or an empty string.</returns>
    public async Task<string> TimeAsync(DateTime? utc)
    {
        if (utc is not { } value)
        {
            return string.Empty;
        }

        var local = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        return local.ToString("g", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Formats a grade.
    /// </summary>
    /// <param name="grade">The grade, from 0 to 100.</param>
    /// <returns>The text, or an empty string.</returns>
    public static string Grade(decimal? grade)
        => grade is { } value ? value.ToString("0.#", CultureInfo.CurrentCulture) : string.Empty;
}
