using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Adds the course and the enrollment of a learner's response to the SurveyResponses data set, so course reports join
/// responses to courses.
/// </summary>
public sealed class LearningSurveyResponseFieldProvider : ISurveyResponseFieldProvider
{
    private readonly ICourseStructureService _courseStructure;
    private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyResponseFieldProvider"/> class.
    /// </summary>
    /// <param name="courseStructure">The course structure service.</param>
    public LearningSurveyResponseFieldProvider(ICourseStructureService courseStructure)
    {
        _courseStructure = courseStructure;
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, string DisplayName, DataFieldType Type)> DescribeFields()
        =>
        [
            ("CourseId", "Course ID", DataFieldType.Text),
            ("CourseTitle", "Course", DataFieldType.Text),
            ("EnrollmentId", "Enrollment ID", DataFieldType.Text),
        ];

    /// <inheritdoc />
    public async Task WriteFieldsAsync(SurveyResponse response, IDictionary<string, object> row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var courseId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseIdProperty);
        var enrollmentId = response?.Context is { } context && string.Equals(context.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal)
            ? context.Id
            : null;

        row["CourseId"] = courseId;
        row["EnrollmentId"] = enrollmentId;
        row["CourseTitle"] = await TitleAsync(courseId);
    }

    private async Task<string> TitleAsync(string courseId)
    {
        if (string.IsNullOrEmpty(courseId))
        {
            return null;
        }

        if (!_titles.TryGetValue(courseId, out var title))
        {
            title = (await _courseStructure.GetCourseAsync(courseId))?.DisplayText;
            _titles[courseId] = title;
        }

        return title;
    }
}
