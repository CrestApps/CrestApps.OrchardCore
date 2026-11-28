using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Tells whether a respondent owns a Learning context: an enrollment is owned by its learner, and a course (the
/// context of anonymous course surveys) by anyone enrolled in it. Learning contexts are accepted only for surveys a
/// course item owns; the enrollment access rule then requires the exact context of the learner's enrollment.
/// </summary>
public sealed class LearningSurveyContextOwnershipCheck : ISurveyContextOwnershipCheck
{
    private readonly IEnrollmentStore _enrollments;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSurveyContextOwnershipCheck"/> class.
    /// </summary>
    /// <param name="enrollments">The enrollment store.</param>
    public LearningSurveyContextOwnershipCheck(IEnrollmentStore enrollments)
    {
        _enrollments = enrollments;
    }

    /// <inheritdoc />
    public bool CanCheck(string kind)
        => string.Equals(kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal)
            || string.Equals(kind, LearningSurveyContexts.CourseKind, StringComparison.Ordinal);

    /// <inheritdoc />
    public async Task<bool> IsOwnerAsync(Survey survey, RecordRef context, SurveyRespondentIdentity identity, CancellationToken cancellationToken = default)
    {
        var userId = identity?.UserId;

        if (string.IsNullOrEmpty(userId) || context.IsEmpty || !LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return false;
        }

        if (string.Equals(context.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal))
        {
            var enrollment = await _enrollments.FindByIdAsync(context.Id, cancellationToken);

            return enrollment is not null && string.Equals(enrollment.UserId, userId, StringComparison.Ordinal);
        }

        if (string.Equals(context.Kind, LearningSurveyContexts.CourseKind, StringComparison.Ordinal))
        {
            var enrollments = await _enrollments.ListByCourseForUserAsync(userId, context.Id, cancellationToken);

            return enrollments.Count > 0;
        }

        return false;
    }
}
