using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Controllers;

/// <summary>
/// The learner pages: My learning, the course player, the transcript, enrolling on a course page, and the calls the
/// player makes to record progress. Every call loads the enrollment by id and checks that it is the caller's own and
/// that the item is open to them; the server decides completion.
/// </summary>
[Feature(LearningConstants.Features.LearnerPages)]
public sealed class LearnerController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly LearnerPageService _pageService;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILearnerProgressService _progressService;
    private readonly ICourseAccessService _courseAccess;
    private readonly ICourseStructureService _courseStructure;
    private readonly ISelfEnrollmentService _selfEnrollment;
    private readonly IContentManager _contentManager;
    private readonly INotifier _notifier;
    private readonly LearningRefusalText _refusalText;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearnerController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="pageService">The learner page builder.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressService">The progress service.</param>
    /// <param name="courseAccess">The course access decision.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="selfEnrollment">The self enrollment service.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="refusalText">The text of refusal codes.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public LearnerController(
        IAuthorizationService authorizationService,
        LearnerPageService pageService,
        IEnrollmentStore enrollmentStore,
        ILearnerProgressService progressService,
        ICourseAccessService courseAccess,
        ICourseStructureService courseStructure,
        ISelfEnrollmentService selfEnrollment,
        IContentManager contentManager,
        INotifier notifier,
        LearningRefusalText refusalText,
        IHtmlLocalizer<LearnerController> htmlLocalizer)
    {
        _refusalText = refusalText;
        _authorizationService = authorizationService;
        _pageService = pageService;
        _enrollmentStore = enrollmentStore;
        _progressService = progressService;
        _courseAccess = courseAccess;
        _courseStructure = courseStructure;
        _selfEnrollment = selfEnrollment;
        _contentManager = contentManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Shows the learner's courses.
    /// </summary>
    /// <param name="status">The state to show: <c>inprogress</c>, <c>notstarted</c>, <c>completed</c>, or all.</param>
    /// <param name="q">The text to find in the course titles and codes.</param>
    /// <param name="view">The view: <c>list</c>, or cards.</param>
    /// <returns>The page.</returns>
    [HttpGet("learning", Name = "LearningMyLearning")]
    public async Task<IActionResult> Index(string status, string q, string view)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        var model = await _pageService.BuildMyLearningAsync(CurrentUserId(), status, q);

        model.View = string.Equals(view, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "cards";

        return View(model);
    }

    /// <summary>
    /// Shows the learner's completed courses.
    /// </summary>
    /// <returns>The page.</returns>
    [HttpGet("learning/transcript", Name = "LearningTranscript")]
    public async Task<IActionResult> Transcript()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        return View(await _pageService.GetTranscriptAsync(CurrentUserId()));
    }

    /// <summary>
    /// Shows the player on a course item.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="itemId">The item, or empty for the first open one.</param>
    /// <param name="updateModelAccessor">The model updater of the request, for the item's display.</param>
    /// <returns>The player.</returns>
    [HttpGet("learning/courses/{courseId}/play/{itemId?}", Name = "LearningPlayer")]
    public async Task<IActionResult> Play(string courseId, string itemId, [FromServices] IUpdateModelAccessor updateModelAccessor)
    {
        var model = await _pageService.BuildPlayerAsync(User, courseId, itemId, updateModelAccessor.ModelUpdater);

        if (model is null)
        {
            return NotFound();
        }

        if (model.Current is not null && model.Access == CourseItemAccessReason.Anonymous)
        {
            return Challenge();
        }

        return View(model);
    }

    /// <summary>
    /// Shows the course completion panel in the player, for a learner who completed the course.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="updateModelAccessor">The model updater of the request.</param>
    /// <returns>The player with the panel, or the player when the course is not completed.</returns>
    [HttpGet("learning/courses/{courseId}/completed", Name = "LearningCourseCompleted")]
    public async Task<IActionResult> Completed(string courseId, [FromServices] IUpdateModelAccessor updateModelAccessor)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var model = await _pageService.BuildPlayerAsync(User, courseId, null, updateModelAccessor.ModelUpdater);

        if (model is null)
        {
            return NotFound();
        }

        if (model.Enrollment is not { Status: EnrollmentStatus.Completed })
        {
            return RedirectToRoute("LearningPlayer", new { courseId });
        }

        model.ShowCompletion = true;
        model.Current = null;
        model.CurrentShape = null;
        model.CompletionLinks = await _pageService.GetCompletionLinksAsync(model.Enrollment);

        return View("Play", model);
    }

    /// <summary>
    /// Enrolls the signed-in learner in a course, as the course allows.
    /// </summary>
    /// <param name="courseId">The course.</param>
    /// <param name="enrollmentKey">The enrollment key, for a course that needs one.</param>
    /// <param name="selfEnrollment">The marker the Self Enrollment feature registers.</param>
    /// <returns>The player, or the course page with a message.</returns>
    [HttpPost("learning/courses/{courseId}/enroll", Name = "LearningEnroll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(string courseId, string enrollmentKey, [FromServices] IEnumerable<SelfEnrollmentMarker> selfEnrollment)
    {
        if (!selfEnrollment.Any())
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var result = await _selfEnrollment.EnrollAsync(CurrentUserId(), courseId, enrollmentKey);

        if (result.Succeeded && result.Enrollment.Status == EnrollmentStatus.Active)
        {
            return RedirectToRoute("LearningPlayer", new { courseId });
        }

        if (result.Succeeded)
        {
            await _notifier.InformationAsync(H["Your request to enroll is sent. You can start once it is approved."]);
        }
        else
        {
            await _notifier.ErrorAsync(result.ErrorCode switch
            {
                EnrollmentErrors.InvalidKey => H["The enrollment key is not right."],
                EnrollmentErrors.TooManyAttempts => H["Too many wrong keys. Try again in 15 minutes."],
                EnrollmentErrors.Closed => H["This course does not take enrollments on its page."],
                EnrollmentErrors.PrerequisitesNotMet => H["Complete the prerequisite courses first."],
                EnrollmentErrors.CourseNotFound => H["The course is not available."],
                _ => H["You could not be enrolled. Try again."],
            });
        }

        return await RedirectToCourseAsync(courseId);
    }

    /// <summary>
    /// Records the time the learner spends on an item. The player calls it when the item opens and then every half minute.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="seconds">The seconds since the last call.</param>
    /// <returns>The item's state.</returns>
    [HttpPost("learning/progress/{enrollmentId}/{itemId}/heartbeat", Name = "LearningHeartbeat")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Heartbeat(string enrollmentId, string itemId, int seconds)
        => ProgressAsync(enrollmentId, itemId, (enrollment, item) => _progressService.RecordAccessAsync(enrollment, item, seconds));

    /// <summary>
    /// Records how much of a video the learner watched.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="itemId">The video lesson.</param>
    /// <param name="percent">The share watched.</param>
    /// <returns>The item's state.</returns>
    [HttpPost("learning/progress/{enrollmentId}/{itemId}/viewed", Name = "LearningViewed")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Viewed(string enrollmentId, string itemId, int percent)
        => ProgressAsync(enrollmentId, itemId, (enrollment, item) => _progressService.RecordViewedAsync(enrollment, item, percent));

    /// <summary>
    /// Marks an item done, for an item the learner completes by marking it done.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="itemId">The item.</param>
    /// <returns>The player.</returns>
    [HttpPost("learning/progress/{enrollmentId}/{itemId}/done", Name = "LearningDone")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Done(string enrollmentId, string itemId)
        => FormProgressAsync(enrollmentId, itemId, (enrollment, item) => _progressService.MarkDoneAsync(enrollment, item));

    /// <summary>
    /// Records that the learner confirms an acknowledgement's statement.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="itemId">The acknowledgement.</param>
    /// <param name="confirm">Whether the learner ticked the statement.</param>
    /// <returns>The player.</returns>
    [HttpPost("learning/progress/{enrollmentId}/{itemId}/acknowledge", Name = "LearningAcknowledge")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Acknowledge(string enrollmentId, string itemId, bool confirm)
    {
        if (!confirm)
        {
            return FormProgressAsync(enrollmentId, itemId, (enrollment, item) => Task.FromResult(new ProgressUpdate { Enrollment = enrollment, RefusedReason = LearningRefusals.ConfirmStatement }));
        }

        return FormProgressAsync(enrollmentId, itemId, (enrollment, item) => _progressService.AcknowledgeAsync(enrollment, item));
    }

    private async Task<IActionResult> ProgressAsync(string enrollmentId, string itemId, Func<Enrollment, string, Task<ProgressUpdate>> record)
    {
        var enrollment = await AuthorizeProgressAsync(enrollmentId, itemId);

        if (enrollment is null)
        {
            return NotFound();
        }

        var update = await record(enrollment, itemId);
        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var required = course is null ? new List<CourseOutlineEntry>() : (await _courseStructure.GetOutlineAsync(course)).RequiredEntries.ToList();

        return Json(new
        {
            accepted = update.RefusedReason is null,

            // Someone else was changing the enrollment at that moment; the player sends the report again.
            retry = update.RefusedReason == LearningRefusals.Busy,
            state = update.Item?.State.ToString(),
            itemCompleted = update.ItemCompleted,
            courseCompleted = update.CourseCompleted,
            progressPercent = update.Enrollment?.ProgressPercent ?? 0,

            // The player shows "3 of 7 complete" beside the bar.
            completedItems = required.Count(entry => update.Progress?.Find(entry.Item.ContentItemId)?.State == CourseItemState.Completed),
            requiredItems = required.Count,
        });
    }

    private async Task<IActionResult> FormProgressAsync(string enrollmentId, string itemId, Func<Enrollment, string, Task<ProgressUpdate>> record)
    {
        var enrollment = await AuthorizeProgressAsync(enrollmentId, itemId);

        if (enrollment is null)
        {
            return NotFound();
        }

        var update = await record(enrollment, itemId);

        if (update.RefusedReason is not null)
        {
            await _notifier.WarningAsync(H["{0}", _refusalText.Get(update.RefusedReason)]);
        }
        else if (update.CourseCompleted)
        {
            // The step that completes the course leads to the completion panel.
            return RedirectToRoute("LearningCourseCompleted", new { courseId = enrollment.CourseId });
        }

        return RedirectToRoute("LearningPlayer", new { courseId = enrollment.CourseId, itemId });
    }

    private async Task<Enrollment> AuthorizeProgressAsync(string enrollmentId, string itemId)
    {
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(enrollmentId) || string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        var enrollment = await _enrollmentStore.FindByIdAsync(enrollmentId);

        // Only the learner records their own progress; anyone else gets the same answer as for an unknown enrollment.
        if (enrollment is null || enrollment.UserId != CurrentUserId())
        {
            return null;
        }

        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var entry = course is null ? null : (await _courseStructure.GetOutlineAsync(course)).Find(itemId);

        if (entry is null || !(await _courseAccess.GetItemAccessAsync(User, entry.Item)).Allowed)
        {
            return null;
        }

        return enrollment;
    }

    private async Task<IActionResult> RedirectToCourseAsync(string courseId)
    {
        var course = await _courseStructure.GetCourseAsync(courseId);

        if (course is null)
        {
            return RedirectToRoute("LearningMyLearning");
        }

        var metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(course);

        return metadata.DisplayRouteValues is null ? RedirectToRoute("LearningMyLearning") : RedirectToRoute(metadata.DisplayRouteValues);
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
