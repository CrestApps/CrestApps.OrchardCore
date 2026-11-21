using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Surveys.Drivers;

/// <summary>
/// The survey panel on the survey item in the learner player: the questions to answer, the learner's own answers once
/// given, and the totals when the survey shows them to learners and the viewer is course staff or a learner the item is
/// open to (never someone signed out or not enrolled). Only the signed-in learner's own enrollment and response are read.
/// </summary>
internal sealed class SurveyPartLearnerDisplayDriver : ContentPartDisplayDriver<SurveyPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly ISurveyResponseService _responses;
    private readonly IClock _clock;

    public SurveyPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        ISurveyResponseService responses,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _courseStructure = courseStructure;
        _enrollments = enrollments;
        _responses = responses;
        _clock = clock;
    }

    public override IDisplayResult Display(SurveyPart part, BuildPartDisplayContext context)
        => Initialize<SurveyPanelViewModel>("SurveyPart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var courseId = _courseStructure.GetCourseId(part.ContentItem);
            var now = _clock.UtcNow;

            model.SurveyId = part.ContentItem.ContentItemId;
            model.Part = part;
            model.SignedIn = !string.IsNullOrEmpty(userId);
            model.Closed = SurveyRules.IsClosed(part, now);

            if (model.SignedIn && !string.IsNullOrEmpty(courseId))
            {
                // Newest first; the user id is checked again so another learner's enrollment is never used.
                var own = (await _enrollments.ListByCourseForUserAsync(userId, courseId))
                    .Where(candidate => string.Equals(candidate.UserId, userId, StringComparison.Ordinal) && string.Equals(candidate.CourseId, courseId, StringComparison.Ordinal))
                    .ToArray();
                var enrollment = own.FirstOrDefault(candidate => candidate.IsOpen)
                    ?? own.FirstOrDefault(candidate => candidate.Status == EnrollmentStatus.Completed)
                    ?? own.FirstOrDefault();

                model.Enrolled = enrollment is not null;

                // A learner who completed the course answers too: feedback at the end of a course is mostly given then.
                model.EnrollmentActive = enrollment?.Status == EnrollmentStatus.Active
                    || (enrollment?.Status == EnrollmentStatus.Completed && enrollment.Grants.Count > 0 && (enrollment.AccessEndsUtc is null || enrollment.AccessEndsUtc > now));
                model.CannotAnswerReason = enrollment?.Status switch
                {
                    EnrollmentStatus.Pending => "Pending",
                    EnrollmentStatus.Suspended => "Suspended",
                    _ when enrollment is not null && !model.EnrollmentActive => "AccessEnded",
                    _ => null,
                };

                if (enrollment is not null)
                {
                    var response = await _responses.FindAsync(enrollment.ItemId, model.SurveyId);

                    model.Response = response is not null && string.Equals(response.UserId, userId, StringComparison.Ordinal) ? response : null;
                }
            }

            model.CanAnswer = model.EnrollmentActive && !model.Closed && (model.Response is null || part.AllowChangeAnswer);

            if (!model.CanAnswer && model.Enrolled && model.CannotAnswerReason is null)
            {
                model.CannotAnswerReason = model.Closed ? "Closed" : model.Response is not null ? "Answered" : "NotOpen";
            }

            // The totals hold everyone's written answers: only course staff, and learners the item is open to, see them.
            if (SurveyRules.LearnerSeesResults(part, model.Response is not null, now) && await CanSeeResultsAsync(model.SurveyId, userId))
            {
                model.Summary = await _responses.SummarizeAsync(model.SurveyId);
            }
        }).Location("Detail", "Content:5");

    private async Task<bool> CanSeeResultsAsync(string surveyId, string userId)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(userId) || user is null)
        {
            return false;
        }

        return await _authorizationService.AuthorizeAsync(user, LearningPermissions.ManageCourses)
            || await _responses.CanSeeResultsAsync(surveyId, userId);
    }
}
