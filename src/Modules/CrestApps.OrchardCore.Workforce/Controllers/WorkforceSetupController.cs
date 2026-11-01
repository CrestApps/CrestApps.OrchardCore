using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The Get Started checklist: what is set up for Workforce and what to do next.
/// </summary>
public sealed class WorkforceSetupController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _featuresManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceSetupController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="featuresManager">The features manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceSetupController(
        IAuthorizationService authorizationService,
        IShellFeaturesManager featuresManager,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        IStringLocalizer<WorkforceSetupController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _featuresManager = featuresManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the checklist.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("workforce/get-started", "WorkforceGetStarted")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTeams))
        {
            return Forbid();
        }

        var features = (await _featuresManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var services = HttpContext.RequestServices;
        var teams = await _teamManager.GetAllAsync(HttpContext.RequestAborted);
        var (workerCount, _) = await _workerManager.SearchAsync(new WorkerQuery(), 0, 1, HttpContext.RequestAborted);
        var (linked, _) = await _workerManager.SearchAsync(new WorkerQuery { HasUser = true }, 0, 1, HttpContext.RequestAborted);
        var model = new WorkforceSetupViewModel { HasRecipes = features.Contains("OrchardCore.Recipes") };

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Build the team tree"],
            Description = S["Regions, sites, departments and teams. Managers of a team also manage the teams under it, and each team gets a Workforce calendar."],
            Status = S["Teams: {0}", teams.Count()],
            Done = teams.Any(),
            Action = S["Teams"],
            Url = Url.Action("Index", "Teams"),
        });
        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Add workers"],
            Description = S["The people who work, with their teams and roles. Link a worker to a user account so they can use My Work."],
            Status = S["Workers: {0} · Linked to a user: {1}", workerCount, linked],
            Done = workerCount > 0,
            Action = S["Workers"],
            Url = Url.Action("Index", "Workers"),
        });

        await AddAsync(model, features, WorkforceConstants.Features.ShiftPlanning, S["Plan shifts"], S["Shift templates, then a schedule for a team: draft it, fill it automatically, check coverage and publish it."], async () =>
        {
            var templates = await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync(HttpContext.RequestAborted);
            var (periods, _) = await services.GetRequiredService<ISchedulePlanningService>().ListPeriodsAsync(null, 0, 1, HttpContext.RequestAborted);

            return (S["Shift templates: {0} · Schedules: {1}", templates.Count(), periods], templates.Any() && periods > 0, S["Schedules"], Url.Action("Index", "Schedules"));
        });

        await AddAsync(model, features, WorkforceConstants.Features.TimeOff, S["Set up time off"], S["The kinds of time off, the policies that accrue them to teams, and allotments that limit how many are off at once."], async () =>
        {
            var types = await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(HttpContext.RequestAborted);
            var policies = await services.GetRequiredService<ITimeOffPolicyManager>().GetAllAsync(HttpContext.RequestAborted);

            return (S["Types: {0} · Policies: {1}", types.Count(), policies.Count()], types.Any() && policies.Any(), S["Time-Off Policies"], Url.Action("Index", "TimeOffPolicies"));
        });

        await AddAsync(model, features, WorkforceConstants.Features.LaborRules, S["Choose labor rules"], S["Rule sets for the limits your teams follow, such as hours a week or rest between shifts. Schedules are checked before they are published."], async () =>
        {
            var sets = await services.GetRequiredService<ILaborRuleSetManager>().GetAllAsync(HttpContext.RequestAborted);

            return (S["Rule sets: {0}", sets.Count()], sets.Any(), S["Labor Rules"], Url.Action("Index", "LaborRuleSets"));
        });

        await AddAsync(model, features, WorkforceConstants.Features.TimeClock, S["Turn on the time clock"], S["Pay periods, a time clock policy, and the teams that use it. Workers then clock in from My Work or a kiosk."], async () =>
        {
            var calendars = await services.GetRequiredService<IPayPeriodCalendarManager>().GetAllAsync(HttpContext.RequestAborted);
            var policies = await services.GetRequiredService<ITimeClockPolicyManager>().GetAllAsync(HttpContext.RequestAborted);
            var clocked = teams.Count(team => !string.IsNullOrEmpty(team.TimeClockPolicyId));

            return (S["Pay periods: {0} · Policies: {1} · Teams with a policy: {2}", calendars.Count(), policies.Count(), clocked], calendars.Any() && policies.Any() && clocked > 0, S["Time Clock Policies"], Url.Action("Index", "TimeClockPolicies"));
        });

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Open My Work"],
            Description = S["The workers' own site, which can be installed like an app: their schedule, time off, shifts, timesheet and notifications."],
            Status = features.Contains(WorkforceConstants.Features.Portal) ? S["On"] : S["Off"],
            Done = features.Contains(WorkforceConstants.Features.Portal) && linked > 0,
            FeatureOff = !features.Contains(WorkforceConstants.Features.Portal),
            Action = S["Open My Work"],
            Url = Url.Content("~/my-work"),
        });

        return View(model);
    }

    private async Task AddAsync(WorkforceSetupViewModel model, HashSet<string> features, string feature, string title, string description, Func<Task<(string Status, bool Done, string Action, string Url)>> check)
    {
        if (!features.Contains(feature))
        {
            model.Steps.Add(new WorkforceSetupStep { Title = title, Description = description, FeatureOff = true, Action = S["Features"], Url = Url.Content("~/Admin/Features?search=Workforce") });

            return;
        }

        var (status, done, action, url) = await check();

        model.Steps.Add(new WorkforceSetupStep { Title = title, Description = description, Status = status, Done = done, Action = action, Url = url });
    }
}
