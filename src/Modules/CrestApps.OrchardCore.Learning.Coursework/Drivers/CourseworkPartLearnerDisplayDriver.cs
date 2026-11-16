using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Coursework.Drivers;

/// <summary>
/// The coursework panel on the coursework item in the learner player: the due date, the learner's latest submission
/// with its grade and feedback, and the form to hand work in, or in again once it is returned. Only the signed-in
/// learner's own enrollment and submissions are read.
/// </summary>
internal sealed class CourseworkPartLearnerDisplayDriver : ContentPartDisplayDriver<CourseworkPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CourseworkLearnerResolver _learnerResolver;
    private readonly ISubmissionService _submissions;
    private readonly ICourseworkContentSource _contentSource;
    private readonly IClock _clock;

    public CourseworkPartLearnerDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        CourseworkLearnerResolver learnerResolver,
        ISubmissionService submissions,
        ICourseworkContentSource contentSource,
        IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _learnerResolver = learnerResolver;
        _submissions = submissions;
        _contentSource = contentSource;
        _clock = clock;
    }

    public override IDisplayResult Display(CourseworkPart part, BuildPartDisplayContext context)
        => Initialize<CourseworkPanelViewModel>("CourseworkPart_Learner", async model =>
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var learner = await _learnerResolver.ResolveAsync(userId, part.ContentItem);

            model.CourseworkId = part.ContentItem.ContentItemId;
            model.Part = part;
            model.SignedIn = !string.IsNullOrEmpty(userId);
            model.Enrolled = learner?.Enrollment is not null;
            model.EnrollmentActive = learner?.Enrollment?.Status == EnrollmentStatus.Active;
            model.PastDue = part.DueUtc is { } due && _clock.UtcNow > DateTime.SpecifyKind(due, DateTimeKind.Utc);

            if (learner?.Enrollment is not null)
            {
                model.Submissions = (await _submissions.ListForEnrollmentAsync(learner.Enrollment.ItemId, model.CourseworkId))
                    .Where(submission => string.Equals(submission.UserId, userId, StringComparison.Ordinal))
                    .ToList();
            }

            var latest = model.Latest;

            model.CanSubmit = model.EnrollmentActive && (latest is null
                || latest.Status == SubmissionStatus.Returned
                || (latest.Status == SubmissionStatus.Submitted && part.AllowResubmission));

            var rubricId = latest?.Status == SubmissionStatus.Graded && !string.IsNullOrEmpty(latest.RubricId) ? latest.RubricId : part.RubricId;

            model.Rubric = string.IsNullOrEmpty(rubricId) ? null : (await _contentSource.GetPublishedAsync(rubricId)).FindPart<RubricPart>();
        }).Location("Detail", "Content:5");
}
