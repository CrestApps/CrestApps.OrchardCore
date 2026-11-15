using System.Security.Claims;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Certifications.Core;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// Reviews the external learning people record for education credits.
/// </summary>
public sealed class ExternalLearningController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IEducationCreditService _credits;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public ExternalLearningController(IAuthorizationService authorizationService, IEducationCreditService credits, INotifier notifier, IHtmlLocalizer<ExternalLearningController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _credits = credits;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [Admin("learning/external-learning", "LearningExternalLearning")]
    public async Task<IActionResult> Index(ExternalLearningState? state, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ReviewExternalLearning))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _credits.PageExternalAsync(state, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeValues = new RouteValueDictionary();

        if (state is not null)
        {
            routeValues["state"] = state.ToString();
        }

        return View(new ExternalLearningListViewModel
        {
            State = state,
            List = new AdminListContext<ExternalLearningSubmission>
            {
                Source = items.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, routeValues),
            },
        });
    }

    [Admin("learning/external-learning/{id}", "LearningExternalLearningReview")]
    public async Task<IActionResult> Review(string id, [FromServices] LearnerUserLookup users)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ReviewExternalLearning))
        {
            return Forbid();
        }

        var submission = await _credits.FindExternalAsync(id, HttpContext.RequestAborted);

        if (submission is null)
        {
            return NotFound();
        }

        return View(new ExternalLearningReviewViewModel
        {
            Id = id,
            Submission = submission,
            UserName = (await users.GetUserNamesAsync([submission.UserId])).TryGetValue(submission.UserId, out var name) ? name : submission.UserId,
            TypeName = (await _credits.FindTypeAsync(submission.CreditTypeId, HttpContext.RequestAborted))?.Name,
            IsOwnLearning = string.Equals(submission.UserId, User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal),
        });
    }

    [HttpPost]
    [ActionName(nameof(Review))]
    [Admin("learning/external-learning/{id}", "LearningExternalLearningReview")]
    public async Task<IActionResult> ReviewPost(string id, ExternalLearningReviewViewModel model, string decision)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCertificationsPermissions.ReviewExternalLearning))
        {
            return Forbid();
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = decision == "accept"
            ? await _credits.AcceptExternalAsync(id, reviewerId, User.Identity?.Name, model.Note, HttpContext.RequestAborted)
            : await _credits.RejectExternalAsync(id, reviewerId, User.Identity?.Name, model.Note, HttpContext.RequestAborted);

        switch (result.Outcome)
        {
            case ExternalLearningReviewOutcome.Reviewed:
                await _notifier.SuccessAsync(result.Submission.State == ExternalLearningState.Accepted ? H["Accepted: the credits are posted."] : H["Rejected."]);
                break;
            case ExternalLearningReviewOutcome.OwnLearning:
                await _notifier.ErrorAsync(H["You can't review your own external learning. Ask another reviewer."]);
                break;
            case ExternalLearningReviewOutcome.CreditsPosted:
                await _notifier.ErrorAsync(H["Credits were already posted for this external learning, so it can't be rejected."]);
                break;
            case ExternalLearningReviewOutcome.Busy:
                await _notifier.ErrorAsync(H["Someone else is reviewing this external learning. Try again in a moment."]);
                break;
            case ExternalLearningReviewOutcome.NotFound:
                return NotFound();
            default:
                await _notifier.ErrorAsync(H["This external learning was already reviewed."]);
                break;
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Opens the evidence of external learning, to reviewers and to the person who recorded it.
    /// </summary>
    [Route("learning/external-learning/{id}/evidence")]
    public async Task<IActionResult> Evidence(string id, [FromServices] IProtectedFileLinks<ExternalLearningEvidence> links)
    {
        var submission = await _credits.FindExternalAsync(id, HttpContext.RequestAborted);

        if (submission?.EvidenceFileId is not { } fileId)
        {
            return NotFound();
        }

        // Opened through the area's links, so the store's own rule is the only path: every policy must allow it, and
        // an area with no policy serves nothing.
        var opened = await links.OpenAsync(links.CreateToken(fileId, submission.ItemId, TimeSpan.FromMinutes(1)), User, HttpContext.RequestAborted);

        if (opened is not { } found)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        return File(found.Content, "application/octet-stream", found.File.FileName);
    }
}
