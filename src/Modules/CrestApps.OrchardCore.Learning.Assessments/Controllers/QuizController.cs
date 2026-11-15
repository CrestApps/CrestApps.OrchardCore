using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
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
        INotifier notifier,
        IHtmlLocalizer<QuizController> htmlLocalizer)
    {
        _attemptService = attemptService;
        _attemptStore = attemptStore;
        _contentSource = contentSource;
        _reviewPolicy = reviewPolicy;
        _courseStructure = courseStructure;
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
    /// Shows an attempt: the questions while it is in progress, then the result and the review the policy allows.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="q">Ignored for a quiz answered one question at a time, which shows the next unanswered question.</param>
    /// <returns>The page.</returns>
    [HttpGet("learning/attempts/{attemptId}", Name = "LearningAttempt")]
    public async Task<IActionResult> Attempt(string attemptId, int? q)
    {
        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null)
        {
            return NotFound();
        }

        if (attempt.IsOpen && attempt.DeadlineUtc is not null)
        {
            attempt = (await _attemptService.SubmitAtDeadlineAsync(attempt.ItemId)).Attempt ?? attempt;
        }

        var quiz = await _contentSource.GetItemAsync(attempt.QuizId);
        var sequential = quiz?.Get<QuizPart>(nameof(QuizPart))?.Navigation == QuizNavigation.Sequential;
        var model = new AttemptPageViewModel
        {
            Attempt = attempt,
            CanReview = _reviewPolicy.CanReview(attempt),
            CanSeeScore = AttemptReviewPolicy.CanSeeScore(attempt),
            Sequential = sequential,
            CourseId = attempt.CourseId,
            Items = attempt.Items,
        };

        if (attempt.IsOpen && sequential)
        {
            // One question at a time means the next unanswered question only: a larger q does not read ahead.
            model.Items = [attempt.Items[AttemptNavigation.CurrentIndex(attempt)]];
        }

        return View(attempt.IsOpen ? "Attempt" : "Result", model);
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
    /// Saves the answers posted and, unless the learner only moves to the next question, submits the attempt.
    /// </summary>
    /// <param name="attemptId">The attempt.</param>
    /// <param name="next">The next question, for a quiz answered one question at a time.</param>
    /// <returns>The result page, or the next question.</returns>
    [HttpPost("learning/attempts/{attemptId}/submit", Name = "LearningAttemptSubmit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string attemptId, int? next)
    {
        var attempt = await FindOwnAsync(attemptId);

        if (attempt is null)
        {
            return NotFound();
        }

        foreach (var item in attempt.Items.Where(item => AttemptFormReader.HasAnswer(Request.Form, item)).ToList())
        {
            var saved = await _attemptService.SaveResponseAsync(attempt.ItemId, attempt.UserId, item.Index, AttemptFormReader.Read(Request.Form, item));

            if (saved.ErrorCode == AttemptErrors.DeadlinePassed)
            {
                await _notifier.WarningAsync(H["The time was up. Your attempt was submitted with the answers saved before the deadline."]);

                return RedirectToRoute("LearningAttempt", new { attemptId });
            }
        }

        if (next is { } index && index < attempt.Items.Count)
        {
            return RedirectToRoute("LearningAttempt", new { attemptId, q = index });
        }

        await _attemptService.SubmitAsync(attempt.ItemId, attempt.UserId);

        return RedirectToRoute("LearningAttempt", new { attemptId });
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
