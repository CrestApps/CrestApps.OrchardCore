using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using CrestApps.OrchardCore.Learning.Surveys.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Surveys.Drivers;

/// <summary>
/// The survey panel on the survey item in the learner player: the questions to answer, the learner's own answers once
/// given, and the totals when the survey shows them to learners. Only the signed-in learner's own enrollment and
/// response are read.
/// </summary>
internal sealed class SurveyPartLearnerDisplayDriver : ContentPartDisplayDriver<SurveyPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollments;
    private readonly ISurveyResponseService _responses;
    private readonly IClock _clock;

    public SurveyPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollments,
        ISurveyResponseService responses,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
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
                var enrollment = own.FirstOrDefault(candidate => candidate.IsOpen) ?? own.FirstOrDefault();

                model.Enrolled = enrollment is not null;
                model.EnrollmentActive = enrollment?.Status == EnrollmentStatus.Active;

                if (enrollment is not null)
                {
                    var response = await _responses.FindAsync(enrollment.ItemId, model.SurveyId);

                    model.Response = response is not null && string.Equals(response.UserId, userId, StringComparison.Ordinal) ? response : null;
                }
            }

            model.CanAnswer = model.EnrollmentActive && !model.Closed && (model.Response is null || part.AllowChangeAnswer);

            if (SurveyRules.LearnerSeesResults(part, model.Response is not null, now))
            {
                model.Summary = await _responses.SummarizeAsync(model.SurveyId);
            }
        }).Location("Detail", "Content:5");
}
