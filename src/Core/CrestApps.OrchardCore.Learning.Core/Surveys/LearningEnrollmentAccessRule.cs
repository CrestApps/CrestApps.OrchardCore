using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Lets only enrolled learners respond to the survey of a course item: the response must be started, resumed and completed
/// through the learner's own active enrollment, with the item published in its course and released to them. A survey's
/// link or the API therefore never opens a course quiz to someone outside the course, nor an unpublished one to anybody.
/// </summary>
public sealed class LearningEnrollmentAccessRule : ISurveyAccessRule
{
    private readonly ILearningSurveyService _learningSurveys;
    private readonly ICourseAccessService _courseAccess;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningEnrollmentAccessRule"/> class.
    /// </summary>
    /// <param name="learningSurveys">The Learning survey service.</param>
    /// <param name="courseAccess">The course access service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public LearningEnrollmentAccessRule(
        ILearningSurveyService learningSurveys,
        ICourseAccessService courseAccess,
        IHttpContextAccessor httpContextAccessor)
    {
        _learningSurveys = learningSurveys;
        _courseAccess = courseAccess;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public string Name => "LearningEnrollment";

    /// <inheritdoc />
    public int Order => 350;

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckStartAsync(SurveyAccessContext context)
        => CheckAsync(context);

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckResumeAsync(SurveyAccessContext context)
        => CheckAsync(context);

    /// <inheritdoc />
    public Task<SurveyAccessDecision> CheckCompleteAsync(SurveyAccessContext context)
        => CheckAsync(context);

    // The same check runs when a response starts, resumes and completes: a learner withdrawn from the course, or an item
    // unpublished or moved out of it meanwhile, stops the response there.
    private async Task<SurveyAccessDecision> CheckAsync(SurveyAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.IsTest || !LearningSurveyContexts.IsContentOwner(context.Survey?.Owner))
        {
            return SurveyAccessDecision.Allow;
        }

        // Ownership is decided whatever the item's publish state, so an unpublished quiz fails closed, never open.
        if (!await _learningSurveys.IsOwnedByCourseItemAsync(context.Survey))
        {
            return SurveyAccessDecision.Allow;
        }

        // Only the published item, in its course, may be answered.
        var item = await _learningSurveys.FindOwnerItemAsync(context.Survey);

        if (item is null)
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.NotOpen);
        }

        var user = _httpContextAccessor.HttpContext?.User;
        var userId = LearningSurveyUsers.GetUserId(user);

        if (string.IsNullOrEmpty(userId) || !string.Equals(userId, context.Identity?.UserId, StringComparison.Ordinal))
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.SignInRequired);
        }

        // The learner must be enrolled and the item released to them now.
        var access = await _courseAccess.GetItemAccessAsync(user, item);

        if (access.Reason != CourseItemAccessReason.Enrolled || access.Enrollment is null)
        {
            return SurveyAccessDecision.Deny(SurveyErrorCode.NotInAudience);
        }

        // The response must belong to this learner's enrollment (or, for an anonymous survey, to its course).
        var expected = context.Survey.Access?.Anonymity == AnonymityMode.Anonymous
            ? LearningSurveyContexts.Course(access.Enrollment.CourseId)
            : LearningSurveyContexts.Enrollment(access.Enrollment.ItemId);

        return Nullable.Equals(context.Context, expected)
            ? SurveyAccessDecision.Allow
            : SurveyAccessDecision.Deny(SurveyErrorCode.NotInAudience);
    }
}
