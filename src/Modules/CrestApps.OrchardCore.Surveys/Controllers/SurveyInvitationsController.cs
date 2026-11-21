using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.Controllers;

/// <summary>
/// Admin pages of invitation collectors: settings, who is invited, sending and reminders.
/// </summary>
[Feature(SurveysConstants.Features.Invitations)]
public sealed class SurveyInvitationsController : Controller
{
    private const int MaxRecipients = 5_000;

    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyInvitationService _invitations;
    private readonly ISurveyAuthorizationService _surveyAuthorization;
    private readonly UserManager<IUser> _userManager;
    private readonly IEnumerable<IUserAudienceService> _audiences;
    private readonly IEnumerable<ICohortService> _cohorts;
    private readonly IShapeFactory _shapeFactory;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsController"/> class.
    /// </summary>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="invitations">The invitation service.</param>
    /// <param name="surveyAuthorization">The survey authorization service.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="audiences">The user audience service, when audiences are on.</param>
    /// <param name="cohorts">The cohort service, when cohorts are on.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The localizer.</param>
    public SurveyInvitationsController(
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        SurveyInvitationStore invitationStore,
        SurveyInvitationService invitations,
        ISurveyAuthorizationService surveyAuthorization,
        UserManager<IUser> userManager,
        IEnumerable<IUserAudienceService> audiences,
        IEnumerable<ICohortService> cohorts,
        IShapeFactory shapeFactory,
        ISiteService siteService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<SurveyInvitationsController> htmlLocalizer)
    {
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _invitationStore = invitationStore;
        _invitations = invitations;
        _surveyAuthorization = surveyAuthorization;
        _userManager = userManager;
        _audiences = audiences;
        _cohorts = cohorts;
        _shapeFactory = shapeFactory;
        _siteService = siteService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the invitations of a collector.
    /// </summary>
    /// <param name="collectorId">The invitation collector.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/invitations/{collectorId}", "SurveyInvitationsIndex")]
    public async Task<IActionResult> Index(string collectorId, PagerParameters pagerParameters)
    {
        var (survey, collector) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var pager = new Pager(pagerParameters, site.PageSize);
        var (count, items) = await _invitationStore.PageByCollectorAsync(collector.ItemId, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new SurveyInvitationsViewModel
        {
            Survey = survey,
            Collector = collector,
            List = new AdminListContext<SurveyInvitation>
            {
                Source = items,
                Pager = await _shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "collectorId", collector.ItemId } }),
            },
        });
    }

    /// <summary>
    /// Shows the form of a new invitation collector.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/{surveyId}/invitations/create", "SurveyInvitationsCreate")]
    public async Task<IActionResult> Create(string surveyId)
    {
        var survey = await AuthorizeSurveyAsync(surveyId);

        if (survey is null)
        {
            return Forbid();
        }

        return View("Settings", new SurveyInvitationSettingsViewModel
        {
            SurveyId = survey.ItemId,
            SurveyTitle = survey.Title,
            Name = H["Invitations"].Value,
        });
    }

    /// <summary>
    /// Creates an invitation collector.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="model">The settings.</param>
    /// <returns>A redirect to the invitations, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("surveys/{surveyId}/invitations/create", "SurveyInvitationsCreate")]
    public async Task<IActionResult> CreatePost(string surveyId, SurveyInvitationSettingsViewModel model)
    {
        var survey = await AuthorizeSurveyAsync(surveyId);

        if (survey is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.SurveyId = survey.ItemId;
            model.SurveyTitle = survey.Title;

            return View("Settings", model);
        }

        var collector = new SurveyCollector
        {
            ItemId = global::OrchardCore.IdGenerator.GenerateId(),
            SurveyId = survey.ItemId,
            Kind = SurveyCollectorKinds.Invitation,
            Token = SurveyNames.NewToken(),
            Status = CollectorStatus.Open,
            CreatedUtc = _clock.UtcNow,
            ModifiedUtc = _clock.UtcNow,
            OwnerId = _userManager.GetUserId(User),
            Author = User.Identity?.Name,
        };

        Apply(collector, model);
        await _collectorStore.CreateAsync(collector, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The invitation list has been created. Invite people to it."]);

        return RedirectToAction(nameof(Invite), new { collectorId = collector.ItemId });
    }

    /// <summary>
    /// Shows the settings of an invitation collector.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/invitations/{collectorId}/settings", "SurveyInvitationsSettings")]
    public async Task<IActionResult> Settings(string collectorId)
    {
        var (survey, collector) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        var settings = SurveyInvitationService.Settings(collector);

        return View(new SurveyInvitationSettingsViewModel
        {
            SurveyId = survey.ItemId,
            SurveyTitle = survey.Title,
            CollectorId = collector.ItemId,
            Name = collector.Name,
            Subject = settings.Subject,
            Message = settings.Message,
            ExpiresAfterDays = settings.ExpiresAfterDays,
            ReminderCount = settings.ReminderCount,
            ReminderEveryDays = settings.ReminderEveryDays,
        });
    }

    /// <summary>
    /// Saves the settings of an invitation collector.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="model">The settings.</param>
    /// <returns>A redirect to the invitations, or the form with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Settings))]
    [Admin("surveys/invitations/{collectorId}/settings", "SurveyInvitationsSettings")]
    public async Task<IActionResult> SettingsPost(string collectorId, SurveyInvitationSettingsViewModel model)
    {
        var (survey, collector) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.SurveyId = survey.ItemId;
            model.SurveyTitle = survey.Title;
            model.CollectorId = collector.ItemId;

            return View(model);
        }

        Apply(collector, model);
        collector.ModifiedUtc = _clock.UtcNow;
        await _collectorStore.UpdateAsync(collector, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The invitation settings have been saved."]);

        return RedirectToAction(nameof(Index), new { collectorId });
    }

    /// <summary>
    /// Shows the form that invites people.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>The page.</returns>
    [Admin("surveys/invitations/{collectorId}/invite", "SurveyInvitationsInvite")]
    public async Task<IActionResult> Invite(string collectorId)
    {
        var (survey, collector) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        return View(await BuildInviteAsync(survey, collector, new SurveyInviteViewModel()));
    }

    /// <summary>
    /// Invites users, audience or cohort members, and e-mail addresses.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <param name="model">Who to invite.</param>
    /// <returns>A redirect to the invitations.</returns>
    [HttpPost]
    [ActionName(nameof(Invite))]
    [Admin("surveys/invitations/{collectorId}/invite", "SurveyInvitationsInvite")]
    public async Task<IActionResult> InvitePost(string collectorId, SurveyInviteViewModel model)
    {
        var (survey, collector) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        var requests = new List<SurveyInvitationRequest>();
        var userIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userName in Split(model.UserNames))
        {
            var user = await _userManager.FindByNameAsync(userName);

            if (user is null)
            {
                ModelState.AddModelError(nameof(model.UserNames), H["No user has the user name {0}.", userName].Value);

                continue;
            }

            var userId = await _userManager.GetUserIdAsync(user);

            if (userIds.Add(userId))
            {
                requests.Add(new SurveyInvitationRequest { UserId = userId, DisplayName = userName });
            }
        }

        if (!string.IsNullOrWhiteSpace(model.Audience) && _audiences.FirstOrDefault() is { } audiences)
        {
            var (_, members) = await audiences.GetMembersAsync(model.Audience, 0, MaxRecipients, HttpContext.RequestAborted);

            foreach (var userId in members.Where(userIds.Add))
            {
                requests.Add(new SurveyInvitationRequest { UserId = userId });
            }
        }

        if (!string.IsNullOrWhiteSpace(model.CohortId) && _cohorts.FirstOrDefault() is { } cohorts)
        {
            var (_, members) = await cohorts.PageMembersAsync(model.CohortId, 0, MaxRecipients, HttpContext.RequestAborted);

            foreach (var member in members.Where(member => userIds.Add(member.UserId)))
            {
                requests.Add(new SurveyInvitationRequest { UserId = member.UserId });
            }
        }

        foreach (var line in (model.Emails ?? string.Empty).Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
            var email = parts.Length == 2 ? parts[1] : parts[0];
            var name = parts.Length == 2 ? parts[0] : null;

            if (!IsEmail(email))
            {
                ModelState.AddModelError(nameof(model.Emails), H["{0} is not an e-mail address.", email].Value);

                continue;
            }

            requests.Add(new SurveyInvitationRequest { Email = email, DisplayName = name });
        }

        if (!ModelState.IsValid || requests.Count == 0)
        {
            if (requests.Count == 0 && ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, H["Choose who to invite."].Value);
            }

            return View(await BuildInviteAsync(survey, collector, model));
        }

        var created = 0;

        foreach (var request in requests.Take(MaxRecipients))
        {
            request.SurveyId = survey.ItemId;
            request.CollectorId = collector.ItemId;

            var result = model.SendNow
                ? await _invitations.InviteAsync(request, HttpContext.RequestAborted)
                : await _invitations.CreateAsync(request, requireRecipient: true, HttpContext.RequestAborted);

            if (result.Succeeded)
            {
                created++;
            }
        }

        await _notifier.SuccessAsync(H["{0} people were invited.", created]);

        return RedirectToAction(nameof(Index), new { collectorId });
    }

    /// <summary>
    /// Sends the invitations that were never sent.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>A redirect to the invitations.</returns>
    [HttpPost]
    [Admin("surveys/invitations/{collectorId}/send", "SurveyInvitationsSend")]
    public async Task<IActionResult> Send(string collectorId)
    {
        var (survey, _) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        var sent = await _invitations.SendPendingAsync(collectorId, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["{0} invitations were sent.", sent]);

        return RedirectToAction(nameof(Index), new { collectorId });
    }

    /// <summary>
    /// Reminds everyone invited who has not completed the survey, now.
    /// </summary>
    /// <param name="collectorId">The collector.</param>
    /// <returns>A redirect to the invitations.</returns>
    [HttpPost]
    [Admin("surveys/invitations/{collectorId}/remind", "SurveyInvitationsRemind")]
    public async Task<IActionResult> Remind(string collectorId)
    {
        var (survey, _) = await AuthorizeAsync(collectorId);

        if (survey is null)
        {
            return Forbid();
        }

        var sent = await _invitations.RemindAsync(collectorId, force: true, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["{0} reminders were sent.", sent]);

        return RedirectToAction(nameof(Index), new { collectorId });
    }

    private async Task<SurveyInviteViewModel> BuildInviteAsync(Survey survey, SurveyCollector collector, SurveyInviteViewModel model)
    {
        model.SurveyId = survey.ItemId;
        model.SurveyTitle = survey.Title;
        model.CollectorId = collector.ItemId;
        model.CollectorName = collector.Name;
        model.Audiences = [new SelectListItem(H["No audience"].Value, string.Empty)];
        model.Cohorts = [new SelectListItem(H["No cohort"].Value, string.Empty)];

        if (_audiences.FirstOrDefault() is { } audiences)
        {
            foreach (var audience in await audiences.ListAsync(HttpContext.RequestAborted))
            {
                model.Audiences.Add(new SelectListItem(audience.DisplayName ?? audience.Name, audience.Name, audience.Name == model.Audience));
            }
        }

        if (_cohorts.FirstOrDefault() is { } cohorts)
        {
            foreach (var cohort in await cohorts.ListAsync(cancellationToken: HttpContext.RequestAborted))
            {
                model.Cohorts.Add(new SelectListItem(cohort.Name, cohort.CohortId, cohort.CohortId == model.CohortId));
            }
        }

        return model;
    }

    private static void Apply(SurveyCollector collector, SurveyInvitationSettingsViewModel model)
    {
        collector.Name = model.Name?.Trim();
        collector.Put(new SurveyInvitationSettings
        {
            Subject = string.IsNullOrWhiteSpace(model.Subject) ? null : model.Subject.Trim(),
            Message = string.IsNullOrWhiteSpace(model.Message) ? null : model.Message,
            ExpiresAfterDays = model.ExpiresAfterDays is > 0 ? model.ExpiresAfterDays : null,
            ReminderCount = Math.Clamp(model.ReminderCount, 0, 10),
            ReminderEveryDays = Math.Clamp(model.ReminderEveryDays, 1, 90),
        });
    }

    private static IEnumerable<string> Split(string value)
        => (value ?? string.Empty).Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool IsEmail(string value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(value, out var address) && address.Address == value;

    private async Task<Survey> AuthorizeSurveyAsync(string surveyId)
    {
        var survey = string.IsNullOrEmpty(surveyId) ? null : await _surveyStore.FindByIdAsync(surveyId, HttpContext.RequestAborted);

        return survey is not null && await _surveyAuthorization.AuthorizeAsync(User, survey, SurveyOperation.ManageInvitations, HttpContext.RequestAborted) ? survey : null;
    }

    private async Task<(Survey Survey, SurveyCollector Collector)> AuthorizeAsync(string collectorId)
    {
        var collector = string.IsNullOrEmpty(collectorId) ? null : await _collectorStore.FindByIdAsync(collectorId, HttpContext.RequestAborted);

        if (collector is null || collector.Kind != SurveyCollectorKinds.Invitation)
        {
            return (null, null);
        }

        var survey = await AuthorizeSurveyAsync(collector.SurveyId);

        return survey is null ? (null, null) : (survey, collector);
    }
}
