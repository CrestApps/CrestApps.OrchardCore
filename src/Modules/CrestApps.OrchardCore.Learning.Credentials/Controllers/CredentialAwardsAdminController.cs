using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Credentials.Controllers;

/// <summary>
/// The admin pages of credential awards: the list, an award by hand, an award's page and its revocation.
/// </summary>
[Admin]
public sealed class CredentialAwardsAdminController : Controller
{
    private readonly ICredentialAwardService _awards;
    private readonly CredentialAwardStore _store;
    private readonly ICredentialDefinitionCatalog _definitions;
    private readonly CredentialRecipientResolver _recipients;
    private readonly CredentialVerifier _verifier;
    private readonly CredentialIssuerContext _issuerContext;
    private readonly LearnerUserLookup _userLookup;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardsAdminController"/> class.
    /// </summary>
    /// <param name="awards">The award service.</param>
    /// <param name="store">The award store.</param>
    /// <param name="definitions">The definitions.</param>
    /// <param name="recipients">The recipients.</param>
    /// <param name="verifier">The verifier.</param>
    /// <param name="issuerContext">The issuer.</param>
    /// <param name="userLookup">The user lookup.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CredentialAwardsAdminController(
        ICredentialAwardService awards,
        CredentialAwardStore store,
        ICredentialDefinitionCatalog definitions,
        CredentialRecipientResolver recipients,
        CredentialVerifier verifier,
        CredentialIssuerContext issuerContext,
        LearnerUserLookup userLookup,
        IAuthorizationService authorizationService,
        ILocalClock localClock,
        IClock clock,
        INotifier notifier,
        IStringLocalizer<CredentialAwardsAdminController> stringLocalizer,
        IHtmlLocalizer<CredentialAwardsAdminController> htmlLocalizer)
    {
        _awards = awards;
        _store = store;
        _definitions = definitions;
        _recipients = recipients;
        _verifier = verifier;
        _issuerContext = issuerContext;
        _userLookup = userLookup;
        _authorizationService = authorizationService;
        _localClock = localClock;
        _clock = clock;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    /// <summary>
    /// The list of awards.
    /// </summary>
    /// <param name="options">The filters.</param>
    /// <param name="pagerParameters">The pager.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("learning/credential-awards", "LearningCredentialAwards")]
    public async Task<IActionResult> Index(CredentialAwardListOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.ViewCredentialAwards))
        {
            return Forbid();
        }

        options ??= new CredentialAwardListOptions();

        var filter = new CredentialAwardAdminFilter
        {
            DefinitionId = Clean(options.Definition),
            Status = options.Status switch
            {
                "active" => CredentialAwardStatus.Active,
                "expired" => CredentialAwardStatus.Expired,
                "revoked" => CredentialAwardStatus.Revoked,
                _ => null,
            },
            IssuedFromUtc = await LocalDayToUtcAsync(options.From, 0),
            IssuedToUtc = await LocalDayToUtcAsync(options.To, 1),
        };

        var search = Clean(options.Search);
        var nothingMatches = false;

        if (search is not null)
        {
            filter.RecipientUserId = await _userLookup.FindUserIdAsync(search);
            nothingMatches = filter.RecipientUserId is null;
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = nothingMatches
            ? (0, [])
            : await _store.PageForAdminAsync(filter, _clock.UtcNow, (pager.Page - 1) * pager.PageSize, pager.PageSize, HttpContext.RequestAborted);

        var names = await _userLookup.GetUserNamesAsync(items.Select(award => award.RecipientUserId));
        var canRevoke = await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials);
        var rows = items.Select(award => new CredentialAwardRow
        {
            Award = award,
            RecipientName = award.RecipientUserId is { } userId && names.TryGetValue(userId, out var name) ? name : null,
            Status = award.GetStatus(_clock.UtcNow),
            CanRevoke = canRevoke && !award.IsRevoked,
        }).ToList();

        var model = new CredentialAwardListViewModel
        {
            Options = options,
            CanAward = canRevoke,
            Definitions = (await _definitions.ListAsync(HttpContext.RequestAborted))
                .OrderBy(definition => definition.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(definition => new SelectListItem(definition.Title, definition.DefinitionId, definition.DefinitionId == filter.DefinitionId))
                .ToList(),
            Statuses =
            [
                new SelectListItem(S["Active"], "active", options.Status == "active"),
                new SelectListItem(S["Expired"], "expired", options.Status == "expired"),
                new SelectListItem(S["Revoked"], "revoked", options.Status == "revoked"),
            ],
        };

        var route = new RouteValueDictionary
        {
            ["Options.Search"] = options.Search,
            ["Options.Definition"] = options.Definition,
            ["Options.Status"] = options.Status,
            ["Options.From"] = options.From,
            ["Options.To"] = options.To,
        };

        model.List = new AdminListContext<CredentialAwardRow>
        {
            Source = rows,
            Pager = await shapeFactory.PagerAsync(pager, count, route),
            ToolbarActions = await shapeFactory.CreateAsync("CredentialAwardListFilters", Arguments.From(new { ListModel = model })),
        };

        return View(model);
    }

    /// <summary>
    /// The form to award a credential by hand.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("learning/credential-awards/new", "LearningCredentialAwardsNew")]
    public async Task<IActionResult> Award()
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials))
        {
            return Forbid();
        }

        var model = new AwardCredentialViewModel();
        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Awards a credential by hand.
    /// </summary>
    /// <param name="model">The form.</param>
    /// <returns>The list, or the form with its errors.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/credential-awards/new", "LearningCredentialAwardsNew")]
    public async Task<IActionResult> Award(AwardCredentialViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(model.DefinitionId))
        {
            ModelState.AddModelError(nameof(model.DefinitionId), S["Choose a credential."]);
        }

        if (string.IsNullOrEmpty(model.UserId))
        {
            ModelState.AddModelError(nameof(model.UserId), S["Choose a person."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _awards.AwardAsync(new CredentialAwardRequest
            {
                DefinitionId = model.DefinitionId,
                UserId = model.UserId,
                RuleKind = CredentialsConstants.RuleKinds.Manual,
                Actor = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Reason = model.Reason,
            }, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                if (result.Created)
                {
                    await _notifier.SuccessAsync(H["The credential was awarded."]);
                }
                else
                {
                    await _notifier.InformationAsync(H["The person already holds a valid award of this credential, so no second one was made."]);
                }

                return RedirectToAction(nameof(Display), new { id = result.Award.ItemId });
            }

            ModelState.AddModelError(string.Empty, Refusal(result.ErrorCode));
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// An award's page: the facts, the check of its credential and the credential as issued.
    /// </summary>
    /// <param name="id">The award id.</param>
    /// <returns>The page.</returns>
    [Admin("learning/credential-awards/{id}", "LearningCredentialAward")]
    public async Task<IActionResult> Display(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.ViewCredentialAwards))
        {
            return Forbid();
        }

        var award = await _awards.FindByIdAsync(id, HttpContext.RequestAborted);

        if (award is null)
        {
            return NotFound();
        }

        var issuer = await _issuerContext.GetAsync();
        var result = await _verifier.VerifyAsync(award, HttpContext.RequestAborted);

        return View(new CredentialAwardDetailViewModel
        {
            Award = award,
            Status = award.GetStatus(_clock.UtcNow),
            Verification = result,
            RecipientName = await _recipients.GetDisplayNameAsync(award.RecipientUserId),
            AwardedByName = await _recipients.GetDisplayNameAsync(award.AwardedBy),
            RevokedByName = await _recipients.GetDisplayNameAsync(award.RevokedBy),
            VerifyUrl = issuer is null ? null : CredentialUrls.Verify(issuer.BaseUrl, award.PublicId),
            CredentialJson = string.IsNullOrEmpty(award.Credential) ? null : JsonNode.Parse(award.Credential).ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            CanRevoke = !award.IsRevoked && await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials),
        });
    }

    /// <summary>
    /// The form to revoke an award.
    /// </summary>
    /// <param name="id">The award id.</param>
    /// <returns>The page.</returns>
    [Admin("learning/credential-awards/{id}/revoke", "LearningCredentialAwardRevoke")]
    public async Task<IActionResult> Revoke(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials))
        {
            return Forbid();
        }

        var award = await _awards.FindByIdAsync(id, HttpContext.RequestAborted);

        if (award is null)
        {
            return NotFound();
        }

        return View(await RevokeModelAsync(award, new RevokeCredentialViewModel()));
    }

    /// <summary>
    /// Revokes an award. The status list and the verification page show it at once.
    /// </summary>
    /// <param name="id">The award id.</param>
    /// <param name="model">The form.</param>
    /// <returns>The award's page, or the form with its errors.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("learning/credential-awards/{id}/revoke", "LearningCredentialAwardRevoke")]
    public async Task<IActionResult> Revoke(string id, RevokeCredentialViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningCredentialsPermissions.AwardCredentials))
        {
            return Forbid();
        }

        var award = await _awards.FindByIdAsync(id, HttpContext.RequestAborted);

        if (award is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), S["Say why the credential is revoked."]);
        }

        if (ModelState.IsValid)
        {
            var result = await _awards.RevokeAsync(award.ItemId, User.FindFirstValue(ClaimTypes.NameIdentifier), model.Reason, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The credential was revoked. Its verification page and the status list show it now."]);

                return RedirectToAction(nameof(Display), new { id = award.ItemId });
            }

            ModelState.AddModelError(string.Empty, Refusal(result.ErrorCode));
        }

        return View(await RevokeModelAsync(award, model));
    }

    private async Task PopulateAsync(AwardCredentialViewModel model)
        => model.Definitions = (await _definitions.ListAsync(HttpContext.RequestAborted))
            .OrderBy(definition => definition.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(definition => new SelectListItem(definition.Title, definition.DefinitionId))
            .ToList();

    private async Task<RevokeCredentialViewModel> RevokeModelAsync(CredentialAward award, RevokeCredentialViewModel model)
    {
        model.AwardId = award.ItemId;
        model.AchievementName = award.Achievement?.Name;
        model.RecipientName = await _recipients.GetDisplayNameAsync(award.RecipientUserId);
        model.AlreadyRevoked = award.IsRevoked;

        return model;
    }

    private string Refusal(string errorCode)
        => errorCode switch
        {
            CredentialAwardErrors.DefinitionNotFound => S["The credential is not published."],
            CredentialAwardErrors.RecipientNotFound => S["The person is not a user of the site."],
            CredentialAwardErrors.RecipientHasNoEmail => S["The person has no e-mail address. A credential names its recipient by a hash of their e-mail address."],
            CredentialAwardErrors.NoIssuerAddress => S["The site's address is unknown. Set the base URL in the site's settings."],
            CredentialAwardErrors.Busy => S["Another credential is being awarded. Try again."],
            CredentialAwardErrors.AlreadyRevoked => S["The credential is already revoked."],
            CredentialAwardErrors.ReasonRequired => S["Say why the credential is revoked."],
            _ => S["The request was refused ({0}).", errorCode],
        };

    private async Task<DateTime?> LocalDayToUtcAsync(string day, int addDays)
    {
        if (!DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(date.AddDays(addDays), DateTimeKind.Unspecified));
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
