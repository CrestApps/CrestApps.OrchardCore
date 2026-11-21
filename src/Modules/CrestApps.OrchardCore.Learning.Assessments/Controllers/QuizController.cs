using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// The pages a learner takes a quiz on. Every request loads the attempt by id and answers 404 unless it is the signed-in
/// person's own.
/// </summary>
public sealed class QuizController : Controller
{
    private readonly IAssessmentAttemptService _attemptService;
    private readonly AssessmentAttemptStore _attemptStore;
    private readonly IAssessmentContentSource _contentSource;
    private readonly AttemptReviewPolicy _reviewPolicy;
    private readonly ICourseStructureService _courseStructure;
    private readonly IEnrollmentStore _enrollmentStore;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizController"/> class.
    /// </summary>
    public QuizController(
        IAssessmentAttemptService attemptService,
        AssessmentAttemptStore attemptStore,
        IAssessmentContentSource contentSource,
        AttemptReviewPolicy reviewPolicy,
        ICourseStructureService courseStructure,
        IEnrollmentStore enrollmentStore,
        INotifier notifier,
        IHtmlLocalizer<QuizController> htmlLocalizer)
    {
        _attemptService = attemptService;
        _attemptStore = attemptStore;
        _contentSource = contentSource;
        _reviewPolicy = reviewPolicy;
        _courseStructure = courseStructure;
        _enrollmentStore = enrollmentStore;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Starts an attempt, or continues the one in progress.
    /// </summary>
    /// <param name="quizId">The quiz.</param>
    /// <param name="accessCode">The access code.</param>
    /// <param name="honor">Whether the honor statement was confirmed.</param>
    /// <returns>The attempt page, or back to the quiz.</returns>
    [HttpPost("learning/quizzes/{quizId}/start", Name = "LearningQuizStart")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string quizId, string accessCode, bool honor)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var result = await _attemptService.StartAsync(new StartAttemptRequest
        {
            UserId = userId,
            User = User,
            QuizId = quizId,
            AccessCode = accessCode,
            HonorConfirmed = honor,
        });

        if (result.Succeeded)
        {
            return RedirectToRoute("LearningAttempt", new { attemptId = result.Attempt.ItemId });
        }

        if (result.ErrorCode == AttemptErrors.NotFound)
        {
            return NotFound();
        }

        await _notifier.WarningAsync(ErrorText(result.ErrorCode));

        var courseId = _courseStructure.GetCourseId(await _contentSource.GetItemAsync(quizId));

        return LocalRedirect(Url.Content(string.IsNullOrEmpty(courseId) ? "~/learning" : $"~/learning/courses/{courseId}/play/{quizId}"));
    }

    /// <summary>
    /// Shows an attempt: a page of questions while it is in progress, then the result and the review the quiz's review
    /// options allow.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="page">The page of a free quiz, from zero. A sequential quiz shows the page the learner is on, so a
    /// page in the address never reads ahead or goes back.</param>
    /// <returns>The page.</returns>
    [HttpGet("learning/attempts/{attemptId}", Name = "LearningAttempt")]
    public async Task<IActionResult> Attempt(string attemptId, int? page)
    {
        // Someone whose sign-in has ended is asked to sign in again, rather than told the attempt does not exist.
        if (User.Identity?.IsAuthenticated != true)
        {
            return Challenge();
        }

        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null)
        {
            return NotFound();
        }

        if (attempt.IsOpen && attempt.DeadlineUtc is not null)
        {
            attempt = (await _attemptService.SubmitAtDeadlineAsync(attempt.ItemId)).Attempt ?? attempt;
        }

        var sequential = await IsSequentialAsync(attempt);
        var model = new AttemptPageViewModel
        {
            Attempt = attempt,
            CanReview = _reviewPolicy.CanReview(attempt),
            CanSeeScore = _reviewPolicy.CanSeeScore(attempt),
            Sequential = sequential,
            CourseId = attempt.CourseId,
        };

        IEnumerable<AttemptItem> shown = attempt.Items;

        if (attempt.IsOpen)
        {
            model.PageCount = AttemptPaging.PageCount(attempt, sequential);
            model.Page = Math.Clamp(sequential ? attempt.CurrentPage : page ?? attempt.CurrentPage, 0, model.PageCount - 1);
            shown = AttemptPaging.ItemsOn(attempt, model.Page, sequential);
        }

        var numbers = Numbers(attempt);
        model.Questions = shown.Select(item =>
        {
            var shows = _reviewPolicy.ShowsFor(attempt, item);
            var answerable = attempt.IsOpen && AttemptNavigation.CanAnswer(attempt, item.Index, sequential);

            return new AttemptQuestionViewModel
            {
                Item = item,
                Number = numbers.GetValueOrDefault(item.Index),
                Shows = shows,
                ReadOnly = !answerable,
                CanCheck = answerable && attempt.FeedbackMode == QuizFeedbackMode.Immediate,
                CheckUrl = Url.RouteUrl("LearningAttemptCheck", new { attemptId = attempt.ItemId, index = item.Index }),
            };
        }).ToList();

        if (attempt.IsOpen)
        {
            // The submit button says how many questions are still unanswered: those of the other pages from the saved
            // answers; the page counts its own as the learner answers.
            var onPage = model.Questions.Select(question => question.Item.Index).ToHashSet();

            model.UnansweredElsewhere = attempt.Items.Count(item => !onPage.Contains(item.Index) && numbers.GetValueOrDefault(item.Index) is not null && !IsAnswered(item.Response));
        }
        else
        {
            await SetNextAsync(model, attempt);
        }

        return View(attempt.IsOpen ? "Attempt" : "Result", model);
    }

    // The result page continues to the item after the quiz in the course outline, or to the completion panel once the
    // course is completed.
    private async Task SetNextAsync(AttemptPageViewModel model, AssessmentAttempt attempt)
    {
        if (string.IsNullOrEmpty(attempt.CourseId))
        {
            return;
        }

        var enrollments = await _enrollmentStore.ListByCourseForUserAsync(attempt.UserId, attempt.CourseId);

        if (enrollments.Any(enrollment => enrollment.Status == EnrollmentStatus.Completed) && !enrollments.Any(enrollment => enrollment.IsOpen))
        {
            model.CourseCompleted = true;

            return;
        }

        var course = await _courseStructure.GetCourseAsync(attempt.CourseId);

        if (course is null)
        {
            return;
        }

        var steps = (await _courseStructure.GetOutlineAsync(course)).Entries.Where(entry => !entry.Inline).ToList();
        var index = steps.FindIndex(entry => string.Equals(entry.Item.ContentItemId, attempt.QuizId, StringComparison.Ordinal));

        if (index >= 0 && index < steps.Count - 1)
        {
            model.NextItemId = steps[index + 1].Item.ContentItemId;
            model.NextItemTitle = steps[index + 1].Item.DisplayText;
        }
    }

    private static bool IsAnswered(AttemptResponse response)
        => response is not null && (response.ChoiceIds.Count > 0 || response.Texts.Any(text => !string.IsNullOrWhiteSpace(text)) || response.Number is not null || response.Pairs.Count > 0);

    /// <summary>
    /// Saves the answers posted and checks one question, for a quiz with immediate feedback: it is scored now and its
    /// answer cannot change afterwards.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="index">The question to check.</param>
    /// <returns>The page of the question.</returns>
    [HttpPost("learning/attempts/{attemptId}/check/{index:int}", Name = "LearningAttemptCheck")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check(string attemptId, int index)
    {
        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null || index < 0 || index >= attempt.Items.Count)
        {
            return NotFound();
        }

        if (await SaveAnswersAsync(attempt, except: index))
        {
            return RedirectToRoute("LearningAttempt", new { attemptId });
        }

        var result = await _attemptService.CheckAsync(attempt.ItemId, attempt.UserId, index, AttemptFormReader.Read(Request.Form, attempt.Items[index]));

        if (result.ErrorCode == AttemptErrors.DeadlinePassed)
        {
            await _notifier.WarningAsync(H["The time was up. Your attempt was submitted with the answers saved before the deadline."]);

            return RedirectToRoute("LearningAttempt", new { attemptId });
        }

        var sequential = await IsSequentialAsync(attempt);

        return RedirectToRoute("LearningAttempt", new { attemptId, page = sequential ? (int?)null : AttemptPaging.PageOf(attempt, index, sequential) });
    }

    /// <summary>
    /// Saves the answer to one question while the learner works.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="index">The question position.</param>
    /// <returns>The state of the attempt.</returns>
    [HttpPost("learning/attempts/{attemptId}/answers/{index:int}", Name = "LearningAttemptAnswer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(string attemptId, int index)
    {
        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null || index < 0 || index >= attempt.Items.Count)
        {
            return NotFound();
        }

        var result = await _attemptService.SaveResponseAsync(attempt.ItemId, attempt.UserId, index, AttemptFormReader.Read(Request.Form, attempt.Items[index]));

        return Json(new
        {
            saved = result.Succeeded,
            error = result.ErrorCode,
            open = result.Attempt?.IsOpen ?? false,
        });
    }

    /// <summary>
    /// Saves the answers posted and, unless the learner only moves to another page, submits the attempt.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="next">The page to move to, from zero; a sequential quiz moves to the next page only.</param>
    /// <returns>The result page, or the page moved to.</returns>
    [HttpPost("learning/attempts/{attemptId}/submit", Name = "LearningAttemptSubmit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string attemptId, int? next)
    {
        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null)
        {
            return NotFound();
        }

        if (await SaveAnswersAsync(attempt, except: null))
        {
            return RedirectToRoute("LearningAttempt", new { attemptId });
        }

        if (next is { } page)
        {
            var moved = await _attemptService.GoToPageAsync(attempt.ItemId, attempt.UserId, page);
            var sequential = await IsSequentialAsync(attempt);

            return RedirectToRoute("LearningAttempt", new { attemptId, page = moved.Succeeded && !sequential ? page : (int?)null });
        }

        await _attemptService.SubmitAsync(attempt.ItemId, attempt.UserId);

        return RedirectToRoute("LearningAttempt", new { attemptId });
    }

    // Saves every answer the form holds; a question that takes no answer now (checked, or on another page of a
    // sequential quiz) is refused by the service and keeps its answer. Returns true when the time was up.
    private async Task<bool> SaveAnswersAsync(AssessmentAttempt attempt, int? except)
    {
        foreach (var item in attempt.Items.Where(item => item.Index != except && AttemptFormReader.HasAnswer(Request.Form, item)).ToList())
        {
            var saved = await _attemptService.SaveResponseAsync(attempt.ItemId, attempt.UserId, item.Index, AttemptFormReader.Read(Request.Form, item));

            if (saved.ErrorCode == AttemptErrors.DeadlinePassed)
            {
                await _notifier.WarningAsync(H["The time was up. Your attempt was submitted with the answers saved before the deadline."]);

                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsSequentialAsync(AssessmentAttempt attempt)
        => (await _contentSource.GetItemAsync(attempt.QuizId))?.Get<QuizPart>(nameof(QuizPart))?.Navigation == QuizNavigation.Sequential;

    // The numbers shown: descriptions are not numbered, so the questions count on without them.
    private static Dictionary<int, int?> Numbers(AssessmentAttempt attempt)
    {
        var numbers = new Dictionary<int, int?>();
        var number = 0;

        foreach (var item in attempt.Items)
        {
            numbers[item.Index] = item.QuestionType == AssessmentsConstants.QuestionTypes.Description ? null : ++number;
        }

        return numbers;
    }

    private async Task<AssessmentAttempt> FindOwnAsync(string attemptId)
    {
        var userId = CurrentUserId();

        if (userId is null || string.IsNullOrEmpty(attemptId))
        {
            return null;
        }

        var attempt = await _attemptStore.FindByIdAsync(attemptId);

        return attempt is not null && string.Equals(attempt.UserId, userId, StringComparison.Ordinal) ? attempt : null;
    }

    private string CurrentUserId()
        => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    private LocalizedHtmlString ErrorText(string errorCode)
        => errorCode switch
        {
            AttemptErrors.NoAccess => H["Enroll in the course to take this quiz."],
            AttemptErrors.Closed => H["This quiz is not open now."],
            AttemptErrors.NoAttemptsLeft => H["You have used every attempt of this quiz."],
            AttemptErrors.TooSoon => H["You can start the next attempt a little later."],
            AttemptErrors.WrongAccessCode => H["The access code is not right."],
            AttemptErrors.TooManyAttempts => H["Too many wrong codes. Try again in 15 minutes."],
            AttemptErrors.HonorRequired => H["Confirm the statement to start."],
            AttemptErrors.NoQuestions => H["This quiz has no questions yet."],
            _ => H["The quiz could not be started. Try again."],
        };
}
