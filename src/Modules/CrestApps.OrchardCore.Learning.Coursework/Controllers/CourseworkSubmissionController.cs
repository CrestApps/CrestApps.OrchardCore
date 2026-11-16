using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Coursework.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Coursework.Controllers;

/// <summary>
/// The learner's side of coursework: handing work in from the coursework panel of the player, and opening a file of a
/// submission. Work is handed in for the signed-in learner only, under their own active enrollment, which the
/// submission service finds; only the coursework item comes from the request.
/// </summary>
public sealed class CourseworkSubmissionController : Controller
{
    /// <summary>
    /// The largest request a hand-in may be: the coursework editor allows at most 10 files of 50 MB.
    /// </summary>
    internal const long MaxRequestBytes = 520L * 1024 * 1024;

    private readonly IAuthorizationService _authorizationService;
    private readonly CourseworkLearnerResolver _learnerResolver;
    private readonly ISubmissionService _submissions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseworkSubmissionController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="learnerResolver">Finds the learner's enrollment for a coursework item.</param>
    /// <param name="submissions">The submission service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CourseworkSubmissionController(
        IAuthorizationService authorizationService,
        CourseworkLearnerResolver learnerResolver,
        ISubmissionService submissions,
        INotifier notifier,
        IHtmlLocalizer<CourseworkSubmissionController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _learnerResolver = learnerResolver;
        _submissions = submissions;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Hands in the learner's work for a coursework item.
    /// </summary>
    /// <param name="courseworkId">The coursework item.</param>
    /// <param name="text">The text.</param>
    /// <param name="files">The files.</param>
    /// <returns>Back to the coursework in the player.</returns>
    [HttpPost("learning/coursework/{courseworkId}/submit", Name = "LearningCourseworkSubmit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Submit(string courseworkId, string text, IList<IFormFile> files)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.AccessLearning))
        {
            return Forbid();
        }

        var learner = await _learnerResolver.ResolveAsync(userId, courseworkId);

        if (learner is null)
        {
            return NotFound();
        }

        var result = await _submissions.SubmitAsync(new SubmitCourseworkRequest
        {
            UserId = userId,
            CourseworkId = learner.Coursework.ContentItemId,
            Text = text,
            Files = (files ?? [])
                .Where(file => file is { Length: > 0 })
                .Select(file => new SubmissionUpload(file.FileName, file.Length, file.OpenReadStream))
                .ToList(),
        }, HttpContext?.RequestAborted ?? default);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(result.Submission?.Late == true
                ? H["Your work was handed in after the due date, so it is marked late."]
                : H["Your work was handed in."]);
        }
        else
        {
            await _notifier.WarningAsync(ErrorText(result.Error));
        }

        return BackToCoursework(learner);
    }

    /// <summary>
    /// Opens a file of a submission, to the learner who handed it in and to the people who may grade it.
    /// </summary>
    /// <param name="id">The submission.</param>
    /// <param name="fileId">The file.</param>
    /// <param name="links">The protected links of submitted files.</param>
    /// <returns>The file.</returns>
    [HttpGet("learning/coursework/submissions/{id}/files/{fileId}", Name = "LearningCourseworkFile")]
    public async Task<IActionResult> Download(string id, string fileId, [FromServices] IProtectedFileLinks<CourseworkFiles> links)
    {
        var submission = await _submissions.FindByIdAsync(id, HttpContext.RequestAborted);

        if (submission is null || string.IsNullOrEmpty(fileId) || !submission.Files.Any(file => file.FileId == fileId))
        {
            return NotFound();
        }

        // Opened through the area's links, so the area's policy is the only path: every policy must allow it.
        var opened = await links.OpenAsync(links.CreateToken(fileId, submission.ItemId, TimeSpan.FromMinutes(1)), User, HttpContext.RequestAborted);

        if (opened is not { } found)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        return File(found.Content, "application/octet-stream", found.File.FileName);
    }

    private LocalizedHtmlString ErrorText(string error)
        => error switch
        {
            SubmissionErrors.NotEnrolled => H["You need an active enrollment in the course to hand in work."],
            SubmissionErrors.NotCoursework => H["This is not coursework of your course."],
            SubmissionErrors.Empty => H["Type your answer or choose a file to hand in."],
            SubmissionErrors.TextRequired => H["Type your answer to hand in."],
            SubmissionErrors.FilesRequired => H["Choose a file to hand in."],
            SubmissionErrors.FilesNotAllowed => H["This coursework takes typed answers only, not files."],
            SubmissionErrors.ExtensionNotAllowed => H["A file is of a type this coursework does not take."],
            SubmissionErrors.FileTooLarge => H["A file is larger than this coursework allows."],
            SubmissionErrors.TooManyFiles => H["You chose more files than this coursework allows."],
            SubmissionErrors.TextTooLong => H["Your answer is too long."],
            SubmissionErrors.FileRefused => H["A file was refused, for instance by the virus scanner. Nothing was handed in."],
            SubmissionErrors.AlreadySubmitted => H["You already handed this work in. You can hand it in again only if a grader returns it."],
            SubmissionErrors.AlreadyGraded => H["Your work is graded, so it cannot be handed in again."],
            SubmissionErrors.Busy => H["Your work is being saved. Try again in a moment."],
            _ => H["Your work could not be handed in."],
        };

    private LocalRedirectResult BackToCoursework(CourseworkLearner learner)
    {
        var url = string.IsNullOrEmpty(learner.CourseId)
            ? null
            : Url?.RouteUrl("LearningPlayer", new { courseId = learner.CourseId, itemId = learner.Coursework.ContentItemId });

        return LocalRedirect(url ?? "~/");
    }
}
