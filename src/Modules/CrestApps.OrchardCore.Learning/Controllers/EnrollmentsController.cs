using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Controllers;

/// <summary>
/// The enrollment pages: the list, enrolling by hand, an enrollment's progress and history, and the changes a person
/// with the permission makes, each with a reason where one is needed.
/// </summary>
public sealed class EnrollmentsController : Controller
{
    // The status filter value that lists completed enrollments whose result changed after completion.
    private const string NeedsReviewFilter = "NeedsReview";

    private readonly IAuthorizationService _authorizationService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly ILearnerProgressService _progressService;
    private readonly ICourseStructureService _courseStructure;
    private readonly EnrollmentListService _listService;
    private readonly CourseEditorOptions _editorOptions;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;
    private readonly EnrollmentSourceOptions _sourceOptions;
    private readonly LearningRefusalText _refusalText;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollmentsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="enrollmentService">The enrollment service.</param>
    /// <param name="enrollmentStore">The enrollment store.</param>
    /// <param name="progressService">The progress service.</param>
    /// <param name="courseStructure">The course structure reader.</param>
    /// <param name="listService">The enrollment list scope.</param>
    /// <param name="editorOptions">The course options of the forms.</param>
    /// <param name="localClock">The local clock, for times people type.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="sourceOptions">The registered enrollment sources.</param>
    /// <param name="refusalText">The text of refusal codes.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EnrollmentsController(
        IAuthorizationService authorizationService,
        IEnrollmentService enrollmentService,
        IEnrollmentStore enrollmentStore,
        ILearnerProgressService progressService,
        ICourseStructureService courseStructure,
        EnrollmentListService listService,
        CourseEditorOptions editorOptions,
        ILocalClock localClock,
        INotifier notifier,
        IOptions<EnrollmentSourceOptions> sourceOptions,
        IHtmlLocalizer<EnrollmentsController> htmlLocalizer,
        LearningRefusalText refusalText,
        IStringLocalizer<EnrollmentsController> stringLocalizer)
    {
        _refusalText = refusalText;
        _authorizationService = authorizationService;
        _enrollmentService = enrollmentService;
        _enrollmentStore = enrollmentStore;
        _progressService = progressService;
        _courseStructure = courseStructure;
        _listService = listService;
        _editorOptions = editorOptions;
        _localClock = localClock;
        _notifier = notifier;
        _sourceOptions = sourceOptions.Value;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the enrollments the person may see.
    /// </summary>
    /// <param name="model">The filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [HttpGet]
    [Admin("learning/enrollments", "LearningEnrollments")]
    public async Task<IActionResult> Index(
        EnrollmentListViewModel model,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var query = new EnrollmentQuery
        {
            CourseId = string.IsNullOrEmpty(model.CourseId) ? null : model.CourseId,
            Status = Enum.TryParse<EnrollmentStatus>(model.Status, out var status) ? status : null,
            NeedsReview = model.Status == NeedsReviewFilter ? true : null,
            Number = string.IsNullOrWhiteSpace(model.Search) ? null : model.Search.Trim(),
        };

        var page = await _listService.PageAsync(User, query, pager.Page, pager.PageSize);

        if (page is null)
        {
            return Forbid();
        }

        // Instructors and assistants enroll learners into their own courses, so they are offered the button too.
        model.CanManage = await _listService.GetEnrollableCourseIdsAsync(User) is not { Count: 0 };
        model.List = new AdminListContext<Enrollment>
        {
            Source = page.Value.Items.ToList(),
            Pager = await shapeFactory.PagerAsync(pager, page.Value.Count, new RouteValueDictionary
            {
                { "search", model.Search },
                { "courseId", model.CourseId },
                { "status", model.Status },
            }),
        };

        // A person who sees only their own courses' enrollments is offered only those courses to filter by.
        var visibleCourses = await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewEnrollments) ? null : query.CourseIds;

        model.CourseOptions = [new SelectListItem(S["All courses"], string.Empty), .. await _editorOptions.GetCourseOptionsAsync(null, [model.CourseId], visibleCourses)];
        model.StatusOptions =
        [
            new SelectListItem(S["Any status"], string.Empty),
            new SelectListItem(S["Waiting for approval"], nameof(EnrollmentStatus.Pending), model.Status == nameof(EnrollmentStatus.Pending)),
            new SelectListItem(S["Active"], nameof(EnrollmentStatus.Active), model.Status == nameof(EnrollmentStatus.Active)),
            new SelectListItem(S["Suspended"], nameof(EnrollmentStatus.Suspended), model.Status == nameof(EnrollmentStatus.Suspended)),
            new SelectListItem(S["Completed"], nameof(EnrollmentStatus.Completed), model.Status == nameof(EnrollmentStatus.Completed)),
            new SelectListItem(S["Access ended"], nameof(EnrollmentStatus.Expired), model.Status == nameof(EnrollmentStatus.Expired)),
            new SelectListItem(S["Withdrawn"], nameof(EnrollmentStatus.Withdrawn), model.Status == nameof(EnrollmentStatus.Withdrawn)),
            new SelectListItem(S["Needs review"], NeedsReviewFilter, model.Status == NeedsReviewFilter),
        ];

        model.List.ToolbarActions = await shapeFactory.CreateAsync("LearningEnrollmentFilters", Arguments.From(new { Model = model }));

        return View(model);
    }

    /// <summary>
    /// Shows the form that enrolls a learner.
    /// </summary>
    /// <param name="courseId">The course to preselect.</param>
    /// <returns>The form.</returns>
    [HttpGet]
    [Admin("learning/enrollments/new", "LearningEnrollmentsCreate")]
    public async Task<IActionResult> Create(string courseId)
    {
        var enrollable = await _listService.GetEnrollableCourseIdsAsync(User);

        if (enrollable is { Count: 0 })
        {
            return Forbid();
        }

        var model = new EnrollViewModel { CourseId = courseId };

        model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(null, [courseId], enrollable);

        return View(model);
    }

    /// <summary>
    /// Enrolls a learner.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>The enrollment, or the form with errors.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/new", "LearningEnrollmentsCreate")]
    public async Task<IActionResult> Create(EnrollViewModel model)
    {
        var enrollable = await _listService.GetEnrollableCourseIdsAsync(User);

        if (enrollable is { Count: 0 })
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.UserId))
        {
            ModelState.AddModelError(nameof(model.UserId), S["Choose the learner."]);
        }
        else if (!await _listService.UserExistsAsync(model.UserId))
        {
            ModelState.AddModelError(nameof(model.UserId), S["The learner does not exist."]);
        }

        if (string.IsNullOrWhiteSpace(model.CourseId))
        {
            ModelState.AddModelError(nameof(model.CourseId), S["Choose the course."]);
        }
        else if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments, new LearningCourseResource(model.CourseId)))
        {
            return Forbid();
        }

        if (ModelState.IsValid)
        {
            var result = await _enrollmentService.EnrollAsync(new EnrollmentRequest
            {
                UserId = model.UserId,
                CourseId = model.CourseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "Manual:" + CurrentUserId(),
                AccessEndsUtc = await ToUtcAsync(model.AccessEnds),
                DueUtc = await ToUtcAsync(model.Due),
                IgnorePrerequisites = model.IgnorePrerequisites,
                Actor = CurrentUserId(),
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(result.Created ? H["The learner is enrolled."] : H["The learner was already enrolled; the enrollment now has this grant too."]);

                return RedirectToAction(nameof(Detail), new { id = result.Enrollment.ItemId });
            }

            ModelState.AddModelError(string.Empty, ErrorText(result.ErrorCode));
        }

        model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(null, [model.CourseId], enrollable);

        return View(model);
    }

    /// <summary>
    /// Shows an enrollment with its progress, grants and history.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("learning/enrollments/{id}", "LearningEnrollmentsDetail")]
    public async Task<IActionResult> Detail(string id, [FromServices] IEnumerable<IEnrollmentReviewActionProvider> reviewActionProviders)
    {
        var enrollment = await _enrollmentStore.FindByIdAsync(id);

        if (enrollment is null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewLearnerRecords, enrollment))
        {
            return Forbid();
        }

        var course = await _courseStructure.GetCourseAsync(enrollment.CourseId);
        var names = await _listService.GetNamesAsync(enrollment.History.Select(change => change.Actor).Append(enrollment.UserId));
        var model = new EnrollmentDetailViewModel
        {
            Enrollment = enrollment,
            LearnerName = names.TryGetValue(enrollment.UserId, out var learnerName) ? learnerName : enrollment.UserId,
            CourseTitle = course?.DisplayText ?? enrollment.Completion?.CourseTitle ?? enrollment.CourseId,
            Outline = course is null ? null : await _courseStructure.GetOutlineAsync(course),
            Progress = await _progressService.GetAsync(enrollment.ItemId),
            ActorNames = names,
            SourceNames = _sourceOptions.Sources.ToDictionary(pair => pair.Key, pair => pair.Value?.Value ?? pair.Key, StringComparer.OrdinalIgnoreCase),
            CanManage = await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments, enrollment),
        };

        model.NeedsStaffApproval = enrollment.Status == EnrollmentStatus.Active
            && course is not null
            && course.TryGet<CoursePart>(out var part)
            && part.CompletionCriteria.Any(rule => rule.Name == LearningRuleNames.StaffApproval)
            && !enrollment.Properties.ContainsKey(LearningRuleNames.StaffApprovalProperty);

        if (model.NeedsStaffApproval && course.TryGet<CoursePart>(out var approvalPart))
        {
            model.ApprovalRole = CompletionApprovers.RoleOf(approvalPart);
            model.CanApproveCompletion = model.CanManage && CompletionApprovers.MayApprove(approvalPart, CurrentUserId());
        }

        if (enrollment.NeedsReview && model.CanManage)
        {
            foreach (var provider in reviewActionProviders)
            {
                model.ReviewActions.AddRange(await provider.GetActionsAsync(enrollment, HttpContext.RequestAborted));
            }
        }

        return View(model);
    }

    /// <summary>
    /// Approves a pending enrollment.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/approve", "LearningEnrollmentsApprove")]
    public async Task<IActionResult> Approve(string id)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        await ReportAsync(await _enrollmentService.ApproveAsync(id, CurrentUserId()), H["The enrollment is approved."]);

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Rejects a pending enrollment.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/reject", "LearningEnrollmentsReject")]
    public async Task<IActionResult> Reject(string id, string reason)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        await ReportAsync(await _enrollmentService.RejectAsync(id, CurrentUserId(), reason), H["The enrollment is rejected."]);

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Moves an enrollment to another state.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <param name="status">The new state.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/status", "LearningEnrollmentsStatus")]
    public async Task<IActionResult> ChangeStatus(string id, EnrollmentStatus status, string reason)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        await ReportAsync(await _enrollmentService.ChangeStatusAsync(id, status, CurrentUserId(), reason), H["The enrollment is changed."]);

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Changes when access ends and when the course is due.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <param name="accessEnds">The new end of access, in the person's time zone.</param>
    /// <param name="due">The new due date, in the person's time zone.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/extend", "LearningEnrollmentsExtend")]
    public async Task<IActionResult> Extend(string id, DateTime? accessEnds, DateTime? due, string reason)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        await ReportAsync(await _enrollmentService.ExtendAsync(id, await ToUtcAsync(accessEnds), await ToUtcAsync(due), CurrentUserId(), reason), H["The dates are changed."]);

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Marks a course item complete by hand.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <param name="itemId">The course item.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/complete-item", "LearningEnrollmentsCompleteItem")]
    public async Task<IActionResult> CompleteItem(string id, string itemId, string reason)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        var update = await _progressService.CompleteItemAsync(enrollment, itemId, CurrentUserId(), reason);

        if (update.RefusedReason is null)
        {
            await _notifier.SuccessAsync(H["The item is marked complete."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The item was not marked complete: {0}", _refusalText.Get(update.RefusedReason)]);
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Records that a member of the course staff approves the learner's completion.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/approve-completion", "LearningEnrollmentsApproveCompletion")]
    public async Task<IActionResult> ApproveCompletion(string id)
    {
        var enrollment = await AuthorizeManageAsync(id);

        if (enrollment is null)
        {
            return Forbid();
        }

        var update = await _progressService.ApproveCompletionAsync(enrollment, CurrentUserId());

        if (update.RefusedReason is null)
        {
            await _notifier.SuccessAsync(update.CourseCompleted ? H["The completion is approved and the course is completed."] : H["The completion is approved. The course completes when the learner meets the other criteria."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The completion was not approved: {0}", _refusalText.Get(update.RefusedReason)]);
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>
    /// Clears the review of a completed enrollment whose result changed after completion.
    /// </summary>
    /// <param name="id">The enrollment.</param>
    /// <param name="note">What the reviewer decided.</param>
    /// <returns>The enrollment.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/enrollments/{id}/clear-review", "LearningEnrollmentsClearReview")]
    public async Task<IActionResult> ClearReview(string id, string note)
    {
        if (await AuthorizeManageAsync(id) is null)
        {
            return Forbid();
        }

        await ReportAsync(await _enrollmentService.ClearReviewAsync(id, CurrentUserId(), note), H["The review is cleared."]);

        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<Enrollment> AuthorizeManageAsync(string id)
    {
        var enrollment = await _enrollmentStore.FindByIdAsync(id);

        if (enrollment is null || !await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments, enrollment))
        {
            return null;
        }

        return enrollment;
    }

    private async Task ReportAsync(EnrollmentResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await _notifier.ErrorAsync(H["{0}", ErrorText(result.ErrorCode)]);
        }
    }

    private string ErrorText(string errorCode)
        => errorCode switch
        {
            EnrollmentErrors.CourseNotFound => S["The course does not exist or is not published."],
            EnrollmentErrors.PrerequisitesNotMet => S["The learner has not completed the prerequisite courses. Tick \"Skip prerequisites\" to enroll anyway."],
            EnrollmentErrors.InvalidTransition => S["The enrollment cannot make this change in its current state."],
            EnrollmentErrors.ReasonRequired => S["Give a reason for this change."],
            EnrollmentErrors.Busy => S["Someone else is changing this enrollment. Try again."],
            EnrollmentErrors.NotFound => S["The enrollment was not found."],
            _ => S["The enrollment could not be changed."],
        };

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<DateTime?> ToUtcAsync(DateTime? local)
        => local is { } value ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)) : null;
}
