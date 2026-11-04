using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracking;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// Sets the Time Tracker stage of each agent state and reason.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.TimeTracker)]
public sealed class ContactCenterTimeStagesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly ITimeStageManager _stageManager;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeStagesController"/> class.
    /// </summary>
    public ContactCenterTimeStagesController(
        IAuthorizationService authorizationService,
        ISiteService siteService,
        ITimeStageManager stageManager,
        IAgentStateReasonCodeManager reasonCodeManager,
        INotifier notifier,
        IHtmlLocalizer<ContactCenterTimeStagesController> htmlLocalizer,
        IStringLocalizer<ContactCenterTimeStagesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _siteService = siteService;
        _stageManager = stageManager;
        _reasonCodeManager = reasonCodeManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the stages of the states and reasons.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("time/contact-center-stages", "WorkforceContactCenterTimeStages")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var settings = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<ContactCenterTimeStages>();

        return View(await BuildAsync(settings));
    }

    /// <summary>
    /// Saves the stages of the states and reasons.
    /// </summary>
    /// <param name="model">The rows.</param>
    /// <returns>A redirect to the page.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("time/contact-center-stages", "WorkforceContactCenterTimeStages")]
    public async Task<IActionResult> IndexPost(ContactCenterTimeStagesViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var stageIds = (await _stageManager.GetAllAsync(HttpContext.RequestAborted)).Select(stage => stage.ItemId).ToHashSet(StringComparer.Ordinal);
        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterTimeStages>();

        Apply(settings.States, model.States, stageIds);
        Apply(settings.Reasons, model.Reasons, stageIds);
        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);
        await _notifier.SuccessAsync(H["The stages of agents' states have been saved."]);

        return RedirectToAction(nameof(Index));
    }

    private static void Apply(Dictionary<string, string> target, IEnumerable<ContactCenterTimeStageRow> rows, HashSet<string> stageIds)
    {
        foreach (var row in rows ?? [])
        {
            if (string.IsNullOrEmpty(row.Key))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(row.StageId) && stageIds.Contains(row.StageId))
            {
                target[row.Key] = row.StageId;
            }
            else
            {
                target.Remove(row.Key);
            }
        }
    }

    private async Task<ContactCenterTimeStagesViewModel> BuildAsync(ContactCenterTimeStages settings)
    {
        var model = new ContactCenterTimeStagesViewModel();

        foreach (var state in Enum.GetValues<AgentPresenceStatus>().Where(state => state != AgentPresenceStatus.Offline))
        {
            model.States.Add(new ContactCenterTimeStageRow
            {
                Key = state.ToString(),
                Label = StateName(state),
                StageId = settings.States.GetValueOrDefault(state.ToString()),
            });
        }

        foreach (var reason in (await _reasonCodeManager.GetAllAsync(HttpContext.RequestAborted)).Where(reason => reason.Enabled).OrderBy(reason => reason.SortOrder).ThenBy(reason => reason.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            model.Reasons.Add(new ContactCenterTimeStageRow
            {
                Key = reason.Name,
                Label = reason.Name,
                Hint = S["A reason for {0}.", StateName(reason.AppliesTo)],
                StageId = settings.Reasons.GetValueOrDefault(reason.Name),
            });
        }

        model.StageOptions = [.. (await _stageManager.GetAllAsync(HttpContext.RequestAborted))
            .Where(stage => stage.Enabled)
            .OrderBy(stage => stage.Position)
            .ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(stage => new SelectListItem(stage.Name, stage.ItemId))];

        return model;
    }

    private string StateName(AgentPresenceStatus state) => state switch
    {
        AgentPresenceStatus.Available => S["Available"],
        AgentPresenceStatus.Reserved => S["Offered work"],
        AgentPresenceStatus.Busy => S["On a call or task"],
        AgentPresenceStatus.WrapUp => S["Wrap-up"],
        AgentPresenceStatus.Break => S["Break"],
        AgentPresenceStatus.RequestBreak => S["Asked for a break"],
        AgentPresenceStatus.Away => S["Away"],
        AgentPresenceStatus.DoNotDisturb => S["Do not disturb"],
        AgentPresenceStatus.Meeting => S["Meeting"],
        AgentPresenceStatus.Training => S["Training"],
        AgentPresenceStatus.AfterHoursUnavailable => S["After hours"],
        _ => state.ToString(),
    };
}
