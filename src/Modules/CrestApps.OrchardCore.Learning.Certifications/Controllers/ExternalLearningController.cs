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

        var reviewed = decision == "accept"
            ? await _credits.AcceptExternalAsync(id, User.Identity?.Name, model.Note, HttpContext.RequestAborted)
            : await _credits.RejectExternalAsync(id, User.Identity?.Name, model.Note, HttpContext.RequestAborted);

        if (reviewed is null)
        {
            await _notifier.ErrorAsync(H["This external learning was already reviewed."]);
        }
        else
        {
            await _notifier.SuccessAsync(reviewed.State == ExternalLearningState.Accepted ? H["Accepted: the credits are posted."] : H["Rejected."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Opens the evidence of external learning, to reviewers and to the person who recorded it.
    /// </summary>
    [Route("learning/external-learning/{id}/evidence")]
    public async Task<IActionResult> Evidence(string id, [FromServices] IProtectedFileStore<ExternalLearningEvidence> files, [FromServices] IEnumerable<IProtectedFileAccessPolicy<ExternalLearningEvidence>> policies)
    {
        var submission = await _credits.FindExternalAsync(id, HttpContext.RequestAborted);

        if (submission?.EvidenceFileId is not { } fileId)
        {
            return NotFound();
        }

        var access = new ProtectedFileAccess { FileId = fileId, ResourceId = submission.ItemId, User = User };

        foreach (var policy in policies)
        {
            if (!await policy.CanReadAsync(access, HttpContext.RequestAborted))
            {
                return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
            }
        }

        var file = await files.FindAsync(fileId, HttpContext.RequestAborted);
        var content = file is null ? null : await files.OpenAsync(fileId, HttpContext.RequestAborted);

        return content is null ? NotFound() : File(content, "application/octet-stream", file.FileName);
    }
}
