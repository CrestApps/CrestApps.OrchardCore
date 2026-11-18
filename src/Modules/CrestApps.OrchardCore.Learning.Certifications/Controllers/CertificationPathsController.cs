using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Certifications.Controllers;

/// <summary>
/// The people on a certification path: where each one stands, starting someone, and checking someone again.
/// </summary>
public sealed class CertificationPathsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICertificationPathService _paths;
    private readonly CertificationPathProgressStore _progress;
    private readonly ICertificationManager _certifications;
    private readonly LearnerUserLookup _users;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CertificationPathsController(
        IAuthorizationService authorizationService,
        ICertificationPathService paths,
        CertificationPathProgressStore progress,
        ICertificationManager certifications,
        LearnerUserLookup users,
        INotifier notifier,
        IHtmlLocalizer<CertificationPathsController> htmlLocalizer,
        IStringLocalizer<CertificationPathsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _paths = paths;
        _progress = progress;
        _certifications = certifications;
        _users = users;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [HttpGet]
    [Admin("learning/certification-paths/{id}/people", "LearningCertificationPathPeople")]
    public async Task<IActionResult> People(string id, CertificationPathState? state, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ViewEnrollments))
        {
            return Forbid();
        }

        var path = await _paths.FindByIdAsync(id);

        if (path is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _progress.PageAsync(path.ItemId, state, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var certification = await _certifications.FindByIdAsync(path.CertificationId);
        var routeValues = new RouteValueDictionary { { "id", id } };

        if (state is not null)
        {
            routeValues["state"] = state.ToString();
        }

        return View(new CertificationPathPeopleViewModel
        {
            Path = path,
            CertificationName = certification?.Name,
            State = state,
            List = new AdminListContext<CertificationPathProgress>
            {
                Source = items.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, routeValues),
            },
            StateOptions =
            [
                new SelectListItem(S["In progress"], nameof(CertificationPathState.InProgress), state == CertificationPathState.InProgress),
                new SelectListItem(S["Certified"], nameof(CertificationPathState.Certified), state == CertificationPathState.Certified),
                new SelectListItem(S["Renewal open"], nameof(CertificationPathState.RenewalOpen), state == CertificationPathState.RenewalOpen),
                new SelectListItem(S["Lapsed"], nameof(CertificationPathState.Lapsed), state == CertificationPathState.Lapsed),
                new SelectListItem(S["Revoked"], nameof(CertificationPathState.Revoked), state == CertificationPathState.Revoked),
            ],
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/certification-paths/{id}/start", "LearningCertificationPathStart")]
    public async Task<IActionResult> Start(string id, string userName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments))
        {
            return Forbid();
        }

        var userId = await _users.FindUserIdAsync(userName);

        if (userId is null)
        {
            await _notifier.ErrorAsync(H["No user is named '{0}'.", userName ?? string.Empty]);

            return RedirectToAction(nameof(People), new { id });
        }

        var result = await _paths.StartAsync(id, userId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["'{0}' is on the path.", userName]);

        return RedirectToAction(nameof(People), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/certification-paths/{id}/check/{userId}", "LearningCertificationPathCheck")]
    public async Task<IActionResult> Check(string id, string userId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments))
        {
            return Forbid();
        }

        var result = await _paths.EvaluateAsync(id, userId, HttpContext.RequestAborted);

        await NotifyAsync(result, result.IssuedRecordId is null ? H["Checked again: nothing new to issue."] : H["The certificate has been issued."]);

        return RedirectToAction(nameof(People), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/certification-paths/revoke-from-review/{enrollmentId}/{recordId}", "LearningCertificationRevokeFromReview")]
    public async Task<IActionResult> RevokeFromReview(string enrollmentId, string recordId, [FromServices] ICertificateIssuer issuer, [FromServices] IEnrollmentStore enrollments, [FromServices] DefaultCertificationPathService paths)
    {
        var enrollment = await enrollments.FindByIdAsync(enrollmentId, HttpContext.RequestAborted);

        if (enrollment is null || !await _authorizationService.AuthorizeAsync(User, LearningPermissions.ManageEnrollments, enrollment))
        {
            return Forbid();
        }

        // Managing this enrollment allows revoking only a certificate that rests on it, while its review is open.
        var attempts = HttpContext.RequestServices.GetService<AssessmentAttemptStore>();

        if (!await paths.CanRevokeFromReviewAsync(
            enrollment,
            recordId,
            attempts is null ? null : async attemptId => (await attempts.FindByIdAsync(attemptId, HttpContext.RequestAborted))?.EnrollmentId,
            HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var result = await issuer.RevokeAsync(recordId, User.Identity?.Name, S["Revoked after the review of enrollment {0}.", enrollment.Number ?? enrollment.ItemId], HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The certificate is revoked. Clear the review when you have recorded your decision."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }

        return Redirect(Url.Action("Detail", "Enrollments", new { area = "CrestApps.OrchardCore.Learning", id = enrollmentId }));
    }

    private async Task NotifyAsync(CertificationPathResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }
}
