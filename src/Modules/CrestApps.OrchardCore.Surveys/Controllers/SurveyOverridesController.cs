using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// The accommodations of a survey: extra time, extra attempts or another window for a person or an audience.
/// </summary>
[Admin]
public sealed class SurveyOverridesController : Controller
{
    private readonly ISurveyOverrideService _overrides;
    private readonly ICatalog<Survey> _surveys;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ISurveyAuthorizationService> _surveyAuthorization;
    private readonly IEnumerable<IUserAudienceService> _audiences;
    private readonly UserManager<IUser> _userManager;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverridesController"/> class.
    /// </summary>
    /// <param name="overrides">The accommodation service.</param>
    /// <param name="surveys">The survey store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="surveyAuthorization">The survey authorization service, when registered.</param>
    /// <param name="audiences">The user audience service, when the Audiences feature is on.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The localizer.</param>
    public SurveyOverridesController(
        ISurveyOverrideService overrides,
        ICatalog<Survey> surveys,
        IAuthorizationService authorizationService,
        IEnumerable<ISurveyAuthorizationService> surveyAuthorization,
        IEnumerable<IUserAudienceService> audiences,
        UserManager<IUser> userManager,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<SurveyOverridesController> htmlLocalizer)
    {
        _overrides = overrides;
        _surveys = surveys;
        _authorizationService = authorizationService;
        _surveyAuthorization = surveyAuthorization;
        _audiences = audiences;
        _userManager = userManager;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the accommodations of a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/{surveyId}/overrides", "SurveyOverridesIndex")]
    public async Task<IActionResult> Index(string surveyId)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(survey))
        {
            return Forbid();
        }

        var rows = new List<SurveyOverrideListItem>();

        foreach (var item in await _overrides.ListAsync(surveyId, HttpContext.RequestAborted))
        {
            rows.Add(new SurveyOverrideListItem
            {
                Override = item,
                AppliesTo = !string.IsNullOrEmpty(item.UserId)
                    ? (await _userManager.FindByIdAsync(item.UserId))?.UserName ?? item.UserId
                    : H["Audience {0}", item.AudienceId].Value,
                GrantedBy = string.IsNullOrEmpty(item.Actor) ? null : (await _userManager.FindByIdAsync(item.Actor))?.UserName,
            });
        }

        return View(new SurveyOverridesViewModel
        {
            SurveyId = surveyId,
            SurveyTitle = survey.Title,
            Survey = survey,
            List = new AdminListContext<SurveyOverrideListItem>
            {
                Source = rows,
            },
        });
    }

    /// <summary>
    /// Shows the form that grants an accommodation.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The page.</returns>
    [HttpGet]
    [Admin("surveys/{surveyId}/overrides/grant", "SurveyOverridesGrant")]
    public async Task<IActionResult> Grant(string surveyId)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(survey))
        {
            return Forbid();
        }

        return View(await BuildAsync(survey, new GrantSurveyOverrideViewModel()));
    }

    /// <summary>
    /// Grants an accommodation.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="model">The accommodation.</param>
    /// <returns>The list, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Grant))]
    [Admin("surveys/{surveyId}/overrides/grant", "SurveyOverridesGrant")]
    public async Task<IActionResult> GrantPost(string surveyId, GrantSurveyOverrideViewModel model)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(survey))
        {
            return Forbid();
        }

        string userId = null;

        if (!string.IsNullOrWhiteSpace(model.UserName))
        {
            var user = await _userManager.FindByNameAsync(model.UserName.Trim());

            if (user is null)
            {
                ModelState.AddModelError(nameof(model.UserName), H["No user has this user name."].Value);
            }
            else
            {
                userId = await _userManager.GetUserIdAsync(user);
            }
        }
        else if (string.IsNullOrEmpty(model.AudienceId))
        {
            ModelState.AddModelError(nameof(model.UserName), H["Enter a user name or pick an audience."].Value);
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), H["Say why the accommodation is granted."].Value);
        }

        if (model.ExtraMinutes < 0 || model.ExtraAttempts < 0)
        {
            ModelState.AddModelError(nameof(model.ExtraMinutes), H["Extra minutes and attempts cannot be negative."].Value);
        }

        DateTime? opensUtc = model.Opens is { } opens ? await _localClock.ToUtcLenientAsync(opens) : null;
        DateTime? closesUtc = model.Closes is { } closes ? await _localClock.ToUtcLenientAsync(closes) : null;

        if (opensUtc is not null && closesUtc is not null && closesUtc <= opensUtc)
        {
            ModelState.AddModelError(nameof(model.Closes), H["The closing time must be after the opening time."].Value);
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildAsync(survey, model));
        }

        var error = await _overrides.GrantAsync(new SurveyOverride
        {
            SurveyId = surveyId,
            UserId = userId,
            AudienceId = userId is null ? model.AudienceId : null,
            Context = !string.IsNullOrWhiteSpace(model.ContextKind) && !string.IsNullOrWhiteSpace(model.ContextId)
                ? new RecordRef(model.ContextKind.Trim(), model.ContextId.Trim())
                : null,
            ExtraMinutes = model.ExtraMinutes,
            ExtraAttempts = model.ExtraAttempts,
            OpensUtc = opensUtc,
            ClosesUtc = closesUtc,
            Reason = model.Reason,
            Actor = _userManager.GetUserId(User),
        }, HttpContext.RequestAborted);

        if (error != SurveyErrorCode.None)
        {
            await _notifier.ErrorAsync(H["The accommodation could not be granted."]);

            return View(await BuildAsync(survey, model));
        }

        await _notifier.SuccessAsync(H["The accommodation has been granted."]);

        return RedirectToAction(nameof(Index), new { surveyId });
    }

    /// <summary>
    /// Revokes an accommodation.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="overrideId">The accommodation.</param>
    /// <param name="auditTrail">The audit trail.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("surveys/{surveyId}/overrides/{overrideId}/revoke", "SurveyOverridesRevoke")]
    public async Task<IActionResult> Revoke(string surveyId, string overrideId, [FromServices] ISurveyAuditTrail auditTrail)
    {
        var survey = await _surveys.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        if (survey is null)
        {
            return NotFound();
        }

        if (!await CanManageAsync(survey))
        {
            return Forbid();
        }

        var existing = (await _overrides.ListAsync(surveyId, HttpContext.RequestAborted)).FirstOrDefault(item => item.ItemId == overrideId);

        if (existing is not null && await _overrides.RevokeAsync(overrideId, HttpContext.RequestAborted))
        {
            await auditTrail.RecordAsync(SurveyAuditEvents.OverrideRevoked, new SurveyAuditEntry
            {
                ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                SurveyId = survey.ItemId,
                SurveyName = survey.Name,
                Reason = existing.Reason,
            }, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The accommodation has been revoked."]);
        }

        return RedirectToAction(nameof(Index), new { surveyId });
    }

    private async Task<GrantSurveyOverrideViewModel> BuildAsync(Survey survey, GrantSurveyOverrideViewModel model)
    {
        model.SurveyId = survey.ItemId;
        model.SurveyTitle = survey.Title;
        model.Audiences = [new SelectListItem(H["No audience"].Value, string.Empty)];

        if (_audiences.FirstOrDefault() is { } audiences)
        {
            foreach (var audience in await audiences.ListAsync(HttpContext.RequestAborted))
            {
                model.Audiences.Add(new SelectListItem(audience.DisplayName ?? audience.Name, audience.Name, audience.Name == model.AudienceId));
            }
        }

        return model;
    }

    private async Task<bool> CanManageAsync(Survey survey)
    {
        if (await _authorizationService.AuthorizeAsync(User, SurveysPermissions.ManageSurveyOverrides))
        {
            return true;
        }

        return _surveyAuthorization.FirstOrDefault() is { } authorization
            && await authorization.AuthorizeAsync(User, survey, SurveyOperation.ManageOverrides, HttpContext.RequestAborted);
    }
}
