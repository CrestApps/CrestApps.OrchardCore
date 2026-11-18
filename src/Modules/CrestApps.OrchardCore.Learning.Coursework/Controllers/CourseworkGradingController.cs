using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Coursework.ViewModels;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Coursework.Controllers;

/// <summary>
/// The grading pages of coursework: the submissions of a coursework item, and one submission with its text, files and
/// the rubric or points to grade it. Who may grade is decided by <see cref="GradingScope"/>, as for quiz attempts:
/// everyone with Grade assessments, or the course's graders, instructors and assistants with Manage the courses you
/// teach. Grading graded work again also needs Override grades and a reason.
/// </summary>
[Admin]
public sealed class CourseworkGradingController : Controller
{
    private readonly ISubmissionService _submissions;
    private readonly ICourseworkContentSource _contentSource;
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;
    private readonly GradingScope _scope;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserSearchService _userSearchService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseworkGradingController"/> class.
    /// </summary>
    public CourseworkGradingController(
        ISubmissionService submissions,
        ICourseworkContentSource contentSource,
        IContentManager contentManager,
        ICourseStructureService courseStructure,
        GradingScope scope,
        IAuthorizationService authorizationService,
        IUserSearchService userSearchService,
        INotifier notifier,
        IHtmlLocalizer<CourseworkGradingController> htmlLocalizer)
    {
        _submissions = submissions;
        _contentSource = contentSource;
        _contentManager = contentManager;
        _courseStructure = courseStructure;
        _scope = scope;
        _authorizationService = authorizationService;
        _userSearchService = userSearchService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the submissions of a coursework item, newest first.
    /// </summary>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [HttpGet]
    [Admin("learning/coursework/{courseworkId}/submissions", "LearningCourseworkSubmissions")]
    public async Task<IActionResult> Index(string courseworkId, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var coursework = string.IsNullOrEmpty(courseworkId) ? null : await _contentManager.GetAsync(courseworkId, VersionOptions.Latest);

        if (coursework is null || !coursework.Has<CourseworkPart>())
        {
            return NotFound();
        }

        var courses = await _scope.GetCourseIdsAsync(User);
        var courseId = _courseStructure.GetCourseId(coursework);

        if (courses is not null && (courseId is null || !courses.Contains(courseId)))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _submissions.ListByCourseworkAsync(coursework.ContentItemId, (pager.Page - 1) * pager.PageSize, pager.PageSize, HttpContext.RequestAborted);
        var names = await NamesAsync(items.Select(item => item.UserId));

        return View(new CourseworkSubmissionsViewModel
        {
            Coursework = coursework,
            List = new AdminListContext<SubmissionRow>
            {
                Source = items.Select(item => new SubmissionRow
                {
                    Submission = item,
                    LearnerName = names.TryGetValue(item.UserId ?? string.Empty, out var name) ? name : item.UserId,
                }).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { ["courseworkId"] = coursework.ContentItemId }),
            },
        });
    }

    /// <summary>
    /// Shows a submission with its text, files and the form to grade it or return it.
    /// </summary>
    /// <param name="id">The submission.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/coursework/submissions/{id}", "LearningCourseworkGradeSubmission")]
    public async Task<IActionResult> Submission(string id)
    {
        var submission = await FindGradableAsync(id);

        if (submission is null)
        {
            return NotFound();
        }

        return View(await BuildAsync(new SubmissionGradingViewModel(), submission, fill: true));
    }

    /// <summary>
    /// Grades a submission.
    /// </summary>
    /// <param name="id">The submission.</param>
    /// <param name="model">The grade.</param>
    /// <returns>Back to the submission.</returns>
    [HttpPost]
    [Admin("learning/coursework/submissions/{id}/grade", "LearningCourseworkGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(string id, SubmissionGradingViewModel model)
    {
        var submission = await FindGradableAsync(id);

        if (submission is null)
        {
            return NotFound();
        }

        // The read above is from before the lock; the service checks the permission again on its fresh read, so work
        // another grader graded meanwhile is not graded again without Override grades.
        var canOverride = await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades);

        if (submission.Status == SubmissionStatus.Graded && !canOverride)
        {
            return Forbid();
        }

        if (!GradePoints.TryParse(model.Points, out var points))
        {
            ModelState.AddModelError(nameof(model.Points), H["The points must be a number, such as 7.5 or 7,5."].Value);

            return View(nameof(Submission), await BuildAsync(model, submission, fill: false));
        }

        var result = await _submissions.GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submission.ItemId,
            Actor = User.FindFirstValue(ClaimTypes.NameIdentifier),
            RubricChoices = model.Choices ?? [],
            Points = points,
            Feedback = model.Feedback,
            Reason = model.Reason,
            CanOverrideGrades = canOverride,
        }, HttpContext.RequestAborted);

        if (result.Error == SubmissionErrors.OverrideRequired)
        {
            return Forbid();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The grade is saved. It counts in the course grade."]);

            return RedirectToAction(nameof(Submission), new { id });
        }

        ModelState.AddModelError(string.Empty, ErrorText(result.Error).Value);

        return View(nameof(Submission), await BuildAsync(model, result.Submission ?? submission, fill: false));
    }

    /// <summary>
    /// Returns a submission to the learner to hand in again.
    /// </summary>
    /// <param name="id">The submission.</param>
    /// <param name="model">The feedback.</param>
    /// <returns>Back to the submission.</returns>
    [HttpPost]
    [Admin("learning/coursework/submissions/{id}/return", "LearningCourseworkReturn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(string id, SubmissionGradingViewModel model)
    {
        var submission = await FindGradableAsync(id);

        if (submission is null)
        {
            return NotFound();
        }

        var canOverride = await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades);

        if (submission.Status == SubmissionStatus.Graded && !canOverride)
        {
            return Forbid();
        }

        var result = await _submissions.ReturnAsync(submission.ItemId, model.Feedback, User.FindFirstValue(ClaimTypes.NameIdentifier), canOverride, HttpContext.RequestAborted);

        if (result.Error == SubmissionErrors.OverrideRequired)
        {
            return Forbid();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The work was returned to the learner to hand in again."]);

            return RedirectToAction(nameof(Submission), new { id });
        }

        ModelState.AddModelError(string.Empty, ErrorText(result.Error).Value);

        return View(nameof(Submission), await BuildAsync(model, result.Submission ?? submission, fill: false));
    }

    private async Task<SubmissionGradingViewModel> BuildAsync(SubmissionGradingViewModel model, Submission submission, bool fill)
    {
        var coursework = await _contentSource.GetPublishedAsync(submission.CourseworkId)
            ?? await _contentManager.GetAsync(submission.CourseworkId, VersionOptions.Latest);
        var part = coursework.FindPart<CourseworkPart>() ?? new CourseworkPart();
        var rubricId = submission.Status == SubmissionStatus.Graded && !string.IsNullOrEmpty(submission.RubricId) ? submission.RubricId : part.RubricId;
        var rubric = string.IsNullOrEmpty(rubricId) ? null : (await _contentSource.GetPublishedAsync(rubricId)).FindPart<RubricPart>();
        var earlier = (await _submissions.ListForEnrollmentAsync(submission.EnrollmentId, submission.CourseworkId, HttpContext.RequestAborted))
            .Where(candidate => candidate.ItemId != submission.ItemId)
            .ToList();

        model.Submission = submission;
        model.Coursework = coursework;
        model.Part = part;
        model.Rubric = rubric;
        model.EarlierAttempts = earlier;
        // Grading or returning work that is already graded needs Override grades, as the posts check, so a grader
        // without it is not offered a form that ends in a refusal.
        model.CanGrade = submission.Current
            && submission.Status != SubmissionStatus.Returned
            && (submission.Status != SubmissionStatus.Graded || await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades));
        (model.DaysLate, model.LatePenaltyPercent) = CourseworkLatePolicy.Resolve(submission, part);
        model.Names = await NamesAsync(submission.History.Select(grade => grade.GraderId)
            .Concat(submission.History.Select(grade => grade.ReplacedBy))
            .Append(submission.GraderId)
            .Append(submission.ReturnedBy)
            .Append(submission.UserId));
        model.LearnerName = model.Names.TryGetValue(submission.UserId ?? string.Empty, out var name) ? name : submission.UserId;

        if (fill)
        {
            model.Feedback = submission.Feedback;
            model.Points = submission.Points?.ToString(CultureInfo.InvariantCulture);
            model.Choices = new Dictionary<string, string>(submission.RubricChoices ?? [], StringComparer.Ordinal);
        }

        foreach (var file in submission.Files)
        {
            model.FileUrls[file.FileId] = Url.RouteUrl("LearningCourseworkFile", new { id = submission.ItemId, fileId = file.FileId });
        }

        return model;
    }

    private async Task<Submission> FindGradableAsync(string id)
    {
        var submission = await _submissions.FindByIdAsync(id, HttpContext.RequestAborted);

        return submission is not null && await _scope.CanGradeAsync(User, submission.UserId, submission.CourseId) ? submission : null;
    }

    private async Task<IDictionary<string, string>> NamesAsync(IEnumerable<string> userIds)
        => (await _userSearchService.GetAsync(userIds.Where(userId => !string.IsNullOrEmpty(userId)).Distinct(StringComparer.Ordinal), HttpContext.RequestAborted))
            .ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

    private LocalizedHtmlString ErrorText(string error)
        => error switch
        {
            SubmissionErrors.ReasonRequired => H["Say why the work is graded again. The earlier grade is kept."],
            SubmissionErrors.InvalidScore => H["Choose a level for every criterion, or give points between zero and the most the work is out of."],
            SubmissionErrors.NotGradable => H["This submission cannot be graded: the learner handed in a newer one, or it is with the learner to hand in again."],
            SubmissionErrors.OwnSubmission => H["Nobody grades their own work."],
            SubmissionErrors.Busy => H["The submission or the learner's enrollment is being changed. Try again in a moment."],
            SubmissionErrors.FeedbackTooLong => H["The feedback is too long: it can be up to {0} characters.", CourseworkConstants.MaxFeedbackLength],
            SubmissionErrors.ReasonTooLong => H["The reason is too long: it can be up to {0} characters.", CourseworkConstants.MaxReasonLength],
            SubmissionErrors.EnrollmentNotActive => H["The learner's enrollment is not active (suspended, ended or withdrawn), so the grade cannot count in the course grade. Nothing was changed. Make the enrollment active, then grade again."],
            SubmissionErrors.NotRecorded => H["The grade could not be recorded in the learner's progress, for instance because the item is no longer in the course. Nothing was changed."],
            _ => H["The submission could not be changed."],
        };
}
