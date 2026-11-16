using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Classes.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Learning.Classes.Controllers;

/// <summary>
/// The forms of the class panel in the learner player: register for a session, accept an offered place and give up a
/// place. Every request acts on the signed-in learner's own open enrollment in the course of the class item; only the
/// class item and the session come from the request.
/// </summary>
public sealed class ClassRegistrationController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ClassLearnerResolver _learnerResolver;
    private readonly IClassRegistrationService _registrations;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassRegistrationController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="learnerResolver">Finds the learner's enrollment for a class item.</param>
    /// <param name="registrations">The class registration service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public ClassRegistrationController(
        IAuthorizationService authorizationService,
        ClassLearnerResolver learnerResolver,
        IClassRegistrationService registrations,
        INotifier notifier,
        IHtmlLocalizer<ClassRegistrationController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _learnerResolver = learnerResolver;
        _registrations = registrations;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Registers the learner for a session of the class, or puts them on its waitlist when it is full.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <param name="sessionId">The session.</param>
    /// <returns>Back to the class in the player.</returns>
    [HttpPost("learning/classes/{classItemId}/register", Name = "LearningClassRegister")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Register(string classItemId, string sessionId)
        => ChangeAsync(
            classItemId,
            (enrollmentId, userId) => _registrations.RegisterAsync(enrollmentId, classItemId, sessionId, userId),
            result => result.Registration?.IsWaiting == true
                ? H["The session is full, so you are on its waitlist. You are told when a place is free."]
                : result.Registration?.Appointment?.Status == AppointmentStatus.Requested
                    ? H["You asked for a place in the session. Someone confirms it soon."]
                    : H["You have a place in the session."]);

    /// <summary>
    /// Accepts the place the learner was offered from the waitlist.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>Back to the class in the player.</returns>
    [HttpPost("learning/classes/{classItemId}/accept", Name = "LearningClassAcceptOffer")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Accept(string classItemId)
        => ChangeAsync(
            classItemId,
            (enrollmentId, userId) => _registrations.AcceptOfferAsync(enrollmentId, classItemId, userId),
            _ => H["You accepted the place in the session."]);

    /// <summary>
    /// Gives up the learner's place, or takes them off the waitlist.
    /// </summary>
    /// <param name="classItemId">The class course item.</param>
    /// <returns>Back to the class in the player.</returns>
    [HttpPost("learning/classes/{classItemId}/cancel", Name = "LearningClassCancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string classItemId)
        => ChangeAsync(
            classItemId,
            (enrollmentId, userId) => _registrations.CancelAsync(enrollmentId, classItemId, "Given up by the learner.", userId),
            _ => H["You gave up your place."]);

    private async Task<IActionResult> ChangeAsync(
        string classItemId,
        Func<string, string, Task<ClassRegistrationResult>> change,
        Func<ClassRegistrationResult, LocalizedHtmlString> successMessage)
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

        var learner = await _learnerResolver.ResolveAsync(userId, classItemId);

        if (learner is null)
        {
            return NotFound();
        }

        if (learner.Enrollment is null)
        {
            await _notifier.WarningAsync(H["You are not enrolled in the course of this class."]);

            return BackToClass(learner);
        }

        var result = await change(learner.Enrollment.ItemId, userId);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(successMessage(result));
        }
        else
        {
            await _notifier.WarningAsync(ErrorText(result.Error));
        }

        return BackToClass(learner);
    }

    private LocalizedHtmlString ErrorText(string error)
        => error switch
        {
            ClassRegistrationErrors.EnrollmentNotFound => H["You are not enrolled in the course of this class."],
            ClassRegistrationErrors.EnrollmentNotActive => H["Your enrollment in this course is not active, so you cannot change your place in a class."],
            ClassRegistrationErrors.NotAClassOfTheCourse => H["This class is not part of your course."],
            ClassRegistrationErrors.SessionNotFound => H["The session was not found or was cancelled."],
            ClassRegistrationErrors.AlreadyRegistered => H["You already have a place in another session of this class. Give it up first."],
            ClassRegistrationErrors.NotRegistered => H["You have no place in this class."],
            ClassRegistrationErrors.NoOffer => H["You have no offered place to accept. The offer may have run out."],
            ClassRegistrationErrors.Busy => H["Your place in this class is being changed. Try again in a moment."],
            _ => H["{0}", error ?? string.Empty],
        };

    private LocalRedirectResult BackToClass(ClassLearner learner)
    {
        var url = string.IsNullOrEmpty(learner.CourseId)
            ? null
            : Url.RouteUrl("LearningPlayer", new { courseId = learner.CourseId, itemId = learner.ClassItem.ContentItemId });

        return LocalRedirect(url ?? Url.Content("~/"));
    }
}
