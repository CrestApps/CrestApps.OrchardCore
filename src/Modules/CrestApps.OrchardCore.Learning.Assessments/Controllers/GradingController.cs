using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// The grading pages: attempts that wait for a grader, one attempt with its answers, and the overrides of a quiz.
/// </summary>
[Admin]
public sealed class GradingController : Controller
{
    private readonly AssessmentAttemptStore _attemptStore;
    private readonly AssessmentOverrideStore _overrideStore;
    private readonly IAssessmentAttemptService _attemptService;
    private readonly IAssessmentContentSource _contentSource;
    private readonly GradingScope _scope;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserSearchService _userSearchService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly IEnumerable<IAttemptObserver> _observers;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradingController"/> class.
    /// </summary>
    public GradingController(
        AssessmentAttemptStore attemptStore,
        AssessmentOverrideStore overrideStore,
        IAssessmentAttemptService attemptService,
        IAssessmentContentSource contentSource,
        GradingScope scope,
        IAuthorizationService authorizationService,
        IUserSearchService userSearchService,
        ILocalClock localClock,
        IClock clock,
        INotifier notifier,
        IEnumerable<IAttemptObserver> observers,
        IHtmlLocalizer<GradingController> htmlLocalizer)
    {
        _observers = observers;
        _attemptStore = attemptStore;
        _overrideStore = overrideStore;
        _attemptService = attemptService;
        _contentSource = contentSource;
        _scope = scope;
        _authorizationService = authorizationService;
        _userSearchService = userSearchService;
        _localClock = localClock;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the attempts that wait for a grader, oldest first.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("learning/grading", "LearningGrading")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        var courses = await _scope.GetCourseIdsAsync(User);

        if (courses is { Count: 0 })
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _attemptStore.PageNeedingGradingAsync(courses, pager.Page, pager.PageSize);

        return View(new GradingListViewModel
        {
            List = new AdminListContext<AssessmentAttempt>
            {
                Source = items.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary()),
            },
        });
    }

    /// <summary>
    /// Shows an attempt with its answers, the key and the grader guidance.
    /// </summary>
    /// <param name="id">The attempt.</param>
    /// <returns>The page.</returns>
    [Admin("learning/grading/attempts/{id}", "LearningGradingAttempt")]
    public async Task<IActionResult> Attempt(string id)
    {
        var attempt = await FindGradableAsync(id);

        if (attempt is null)
        {
            return NotFound();
        }

        var model = new AttemptGradingViewModel
        {
            Attempt = attempt,
            CanOverride = await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades),
            CanManageOverrides = await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageAssessmentOverrides),
        };

        foreach (var question in await _contentSource.GetQuestionsAsync(attempt.Items.Where(item => item.QuestionType == AssessmentsConstants.QuestionTypes.Essay).Select(item => item.QuestionId)))
        {
            var guidance = question.Get<EssayQuestionPart>(nameof(EssayQuestionPart))?.GraderGuidance;

            if (!string.IsNullOrEmpty(guidance))
            {
                model.Guidance[question.ContentItemId] = guidance;
            }
        }

        var people = (await _userSearchService.GetAsync(attempt.ScoreHistory.Select(change => change.Actor).Append(attempt.UserId).Where(actor => !string.IsNullOrEmpty(actor)).Distinct(StringComparer.Ordinal)))
            .ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

        model.ActorNames = people;
        model.LearnerName = people.TryGetValue(attempt.UserId, out var name) ? name : attempt.UserId;

        return View(model);
    }

    /// <summary>
    /// Records the scores of a grader.
    /// </summary>
    /// <param name="id">The attempt.</param>
    /// <param name="feedback">The overall feedback.</param>
    /// <returns>Back to the attempt.</returns>
    [HttpPost]
    [Admin("learning/grading/attempts/{id}/grade", "LearningGradingGrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(string id, string feedback)
    {
        var attempt = await FindGradableAsync(id);

        if (attempt is null)
        {
            return NotFound();
        }

        if (attempt.Status == AttemptStatus.Graded && !await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades))
        {
            return Forbid();
        }

        var scores = new Dictionary<int, ManualScore>();

        foreach (var item in attempt.Items)
        {
            var raw = Request.Form["score" + item.Index].ToString();

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var points)
                && !decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out points))
            {
                await _notifier.ErrorAsync(H["The score of question {0} is not a number.", item.Index + 1]);

                return RedirectToAction(nameof(Attempt), new { id });
            }

            if (item.ManualScore != points || item.QuestionType == AssessmentsConstants.QuestionTypes.Essay)
            {
                scores[item.Index] = new ManualScore { Points = points, Comment = Request.Form["comment" + item.Index].ToString() };
            }
        }

        var result = await _attemptService.GradeAsync(id, scores, feedback, User.FindFirstValue(ClaimTypes.NameIdentifier));

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The scores are saved."]);
        }
        else
        {
            await _notifier.ErrorAsync(result.ErrorCode == AttemptErrors.InvalidScore ? H["A score is below zero or above the points of its question."] : H["The scores could not be saved."]);
        }

        return RedirectToAction(nameof(Attempt), new { id });
    }

    /// <summary>
    /// Scores an attempt again from its own items, keeping the old score.
    /// </summary>
    /// <param name="id">The attempt.</param>
    /// <param name="reason">Why.</param>
    /// <returns>Back to the attempt.</returns>
    [HttpPost]
    [Admin("learning/grading/attempts/{id}/regrade", "LearningGradingRegrade")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regrade(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades))
        {
            return Forbid();
        }

        var result = await _attemptService.RegradeAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), reason);

        await NotifyAsync(result, H["The attempt was scored again."]);

        return RedirectToAction(nameof(Attempt), new { id });
    }

    /// <summary>
    /// Voids an attempt; it no longer counts.
    /// </summary>
    /// <param name="id">The attempt.</param>
    /// <param name="reason">Why.</param>
    /// <returns>Back to the attempt.</returns>
    [HttpPost]
    [Admin("learning/grading/attempts/{id}/void", "LearningGradingVoid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.OverrideGrades))
        {
            return Forbid();
        }

        var result = await _attemptService.VoidAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), reason);

        await NotifyAsync(result, H["The attempt was cancelled."]);

        return RedirectToAction(nameof(Attempt), new { id });
    }

    /// <summary>
    /// Lists the overrides of a quiz, with the form that adds one.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <returns>The page.</returns>
    [Admin("learning/quizzes/{quizId}/overrides", "LearningQuizOverrides")]
    public async Task<IActionResult> Overrides(string quizId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageAssessmentOverrides))
        {
            return Forbid();
        }

        var quiz = await _contentSource.GetItemAsync(quizId);

        if (quiz is null)
        {
            return NotFound();
        }

        return View(await BuildOverridesAsync(new QuizOverridesViewModel(), quiz));
    }

    /// <summary>
    /// Adds an override.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="model">The override.</param>
    /// <param name="userManager">The user manager.</param>
    /// <returns>Back to the overrides.</returns>
    [HttpPost]
    [Admin("learning/quizzes/{quizId}/overrides", "LearningQuizOverridesAdd")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOverride(string quizId, QuizOverridesViewModel model, [FromServices] UserManager<IUser> userManager)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageAssessmentOverrides))
        {
            return Forbid();
        }

        var quiz = await _contentSource.GetItemAsync(quizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var user = string.IsNullOrWhiteSpace(model.UserName) ? null : await userManager.FindByNameAsync(model.UserName.Trim());

        if (user is null)
        {
            ModelState.AddModelError(nameof(model.UserName), H["No user has this user name."].Value);
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), H["Say why the override is given."].Value);
        }

        if (model.ExtraMinutes < 0 || model.ExtraAttempts < 0)
        {
            ModelState.AddModelError(nameof(model.ExtraMinutes), H["Extra time and attempts cannot be negative."].Value);
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Overrides), await BuildOverridesAsync(model, quiz));
        }

        var entry = new AssessmentOverride
        {
            UserId = await userManager.GetUserIdAsync(user),
            QuizId = quiz.ContentItemId,
            ExtraMinutes = model.ExtraMinutes,
            ExtraAttempts = model.ExtraAttempts,
            OpensUtc = model.Opens is { } opens ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(opens, DateTimeKind.Unspecified)) : null,
            ClosesUtc = model.Closes is { } closes ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(closes, DateTimeKind.Unspecified)) : null,
            Reason = model.Reason.Trim(),
            Actor = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedUtc = _clock.UtcNow,
        };

        await _overrideStore.CreateAsync(entry);

        foreach (var observer in _observers)
        {
            await observer.OverrideGrantedAsync(entry);
        }

        await _notifier.SuccessAsync(H["The override is saved. It applies to the next attempt of the learner."]);

        return RedirectToAction(nameof(Overrides), new { quizId });
    }

    private async Task<QuizOverridesViewModel> BuildOverridesAsync(QuizOverridesViewModel model, ContentItem quiz)
    {
        model.QuizId = quiz.ContentItemId;
        model.QuizTitle = quiz.DisplayText;
        model.Overrides = (await _overrideStore.ListByQuizAsync(quiz.ContentItemId)).ToList();
        model.Names = (await _userSearchService.GetAsync(model.Overrides.SelectMany(entry => new[] { entry.UserId, entry.Actor }).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)))
            .ToDictionary(user => user.UserId, user => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName, StringComparer.Ordinal);

        return model;
    }

    private async Task<AssessmentAttempt> FindGradableAsync(string id)
    {
        var attempt = string.IsNullOrEmpty(id) ? null : await _attemptStore.FindByIdAsync(id);

        return attempt is not null && await _scope.CanGradeAsync(User, attempt) ? attempt : null;
    }

    private async Task NotifyAsync(AttemptResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);
        }
        else
        {
            await _notifier.ErrorAsync(result.ErrorCode == AttemptErrors.ReasonRequired ? H["Say why."] : H["The attempt could not be changed."]);
        }
    }
}
