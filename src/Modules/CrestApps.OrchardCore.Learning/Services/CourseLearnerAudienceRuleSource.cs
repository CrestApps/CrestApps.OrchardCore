using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// An audience rule that matches the people enrolled in some courses, active or completed.
/// </summary>
internal sealed class CourseLearnerAudienceRuleSource : IUserAudienceRuleSource
{
    public const string SourceName = "CourseLearner";

    private const int PageSize = 500;

    private readonly IEnrollmentStore _enrollments;
    private readonly CourseEditorOptions _courseOptions;

    internal readonly IStringLocalizer S;

    public CourseLearnerAudienceRuleSource(IEnrollmentStore enrollments, CourseEditorOptions courseOptions, IStringLocalizer<CourseLearnerAudienceRuleSource> stringLocalizer)
    {
        _enrollments = enrollments;
        _courseOptions = courseOptions;
        S = stringLocalizer;
    }

    public string Name => SourceName;

    public LocalizedString DisplayName => S["Enrolled in a course"];

    public async Task<IReadOnlyList<UserAudienceRuleOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _courseOptions.GetCourseOptionsAsync(null, null))
            .Select(option => new UserAudienceRuleOption(option.Value, option.Text))
            .ToArray();

    public async Task<IReadOnlyCollection<string>> ResolveUserIdsAsync(UserAudienceRule rule, CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        if (rule.Values.Count == 0)
        {
            return ids;
        }

        foreach (var status in new[] { EnrollmentStatus.Active, EnrollmentStatus.Completed })
        {
            var query = new EnrollmentQuery { CourseIds = rule.Values, Status = status };

            for (var page = 1; ; page++)
            {
                var (count, items) = await _enrollments.PageAsync(query, page, PageSize, cancellationToken);

                ids.UnionWith(items.Select(enrollment => enrollment.UserId).Where(userId => !string.IsNullOrEmpty(userId)));

                if (items.Count < PageSize || page * PageSize >= count)
                {
                    break;
                }
            }
        }

        return ids;
    }

    public async Task<bool> IsMatchAsync(UserAudienceRule rule, string userId, CancellationToken cancellationToken = default)
        => (await _enrollments.ListByUserAsync(userId, cancellationToken))
            .Any(enrollment => rule.Values.Contains(enrollment.CourseId) && enrollment.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed);
}
