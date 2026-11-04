using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The field service Get Started checklist: each part of the setup in order, with what is there now. It opens from the
/// dispatch board, not the menu.
/// </summary>
public sealed class FieldServiceSetupController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _featuresManager;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceSetupController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="featuresManager">The features manager.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FieldServiceSetupController(IAuthorizationService authorizationService, IShellFeaturesManager featuresManager, ISiteService siteService, IStringLocalizer<FieldServiceSetupController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _featuresManager = featuresManager;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the checklist.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("dispatch/get-started", "FieldServiceGetStarted")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatchSettings))
        {
            return Forbid();
        }

        var services = HttpContext.RequestServices;
        var token = HttpContext.RequestAborted;
        var features = (await _featuresManager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);
        var geo = (await _siteService.GetSiteSettingsAsync()).GetOrCreate<GeoSettings>();
        var dispatch = await services.GetRequiredService<IDispatchSettingsProvider>().GetAsync();
        var territories = await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync(token);
        var workerManager = services.GetRequiredService<IWorkerManager>();
        var (workers, _) = await workerManager.SearchAsync(new WorkerQuery(), 0, 1, token);
        var (linked, _) = await workerManager.SearchAsync(new WorkerQuery { HasUser = true }, 0, 1, token);
        var types = await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync(token);
        var checklists = await services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync(token);
        var levels = await services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>().GetAllAsync(token);
        var model = new WorkforceSetupViewModel { HasRecipes = features.Contains("OrchardCore.Recipes") };

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Choose maps and geocoding"],
            Description = S["The provider that finds addresses on the map, and the one that works out travel times. Without one, travel is estimated in a straight line."],
            Status = string.IsNullOrEmpty(geo.GeocodingProvider)
                ? S["No geocoding provider yet."]
                : S["Geocoding: {0} · Travel times: {1}", geo.GeocodingProvider, string.IsNullOrEmpty(geo.TravelTimeProvider) ? S["the built-in estimate"] : geo.TravelTimeProvider],
            Done = !string.IsNullOrEmpty(geo.GeocodingProvider),
            Action = S["Maps and Geocoding"],
            Url = Url.Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = GeoConstants.SettingsGroupId }),
        });
        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Draw service territories"],
            Description = S["The areas you serve, by shape or postal code, each with the technicians and teams who serve it."],
            Status = S["Territories: {0}", territories.Count()],
            Done = territories.Any(),
            Action = S["Territories"],
            Url = Url.Action("Index", "Territories", new { area = "CrestApps.OrchardCore.Geo" }),
        });
        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Add the technicians"],
            Description = S["Workers on a field team, with their skills, certifications and home base. Link each to a user account so they can use the Field page."],
            Status = S["Workers: {0} · Linked to a user: {1}", workers, linked],
            Done = workers > 0,
            Action = S["Workers"],
            Url = Url.Action("Index", "Workers", new { area = WorkforceConstants.Features.Workforce }),
        });

        await AddAsync(model, features, WorkforceConstants.Features.ShiftPlanning, S["Publish their shifts"], S["Technicians are offered visits only on a published shift, less their breaks and time off."], () =>
            Task.FromResult((S["Under Calendars > Shift Schedules."].Value, false, S["Shift Schedules"].Value, Url.Action("Index", "Schedules", new { area = WorkforceConstants.Features.Workforce }))));

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Describe the jobs"],
            Description = S["Work order types with their length, crew, skills, checklists and billing; checklists with the questions asked on site; and service levels with the response and resolution times."],
            Status = S["Types: {0} · Checklists: {1} · Service levels: {2}", types.Count(), checklists.Count(), levels.Count()],
            Done = types.Any(),
            Action = S["Work Order Types"],
            Url = Url.Action("Index", "WorkOrderSettings", new { area = "CrestApps.OrchardCore.WorkOrders", kind = "types" }),
        });

        await AddAsync(model, features, "CrestApps.OrchardCore.Assets", S["Record the assets"], S["The equipment you service at customers' sites, and with Fleet, the vans and tools you book for visits, each van with its own stock."], async () =>
        {
            var assets = await services.GetRequiredService<AssetManager>().GetAllAsync(token);

            return (S["Assets: {0}", assets.Count()].Value, assets.Any(), S["Assets"].Value, Url.Action("Index", "Assets", new { area = "CrestApps.OrchardCore.Assets" }));
        });

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Tell customers"],
            Description = S["Texts or emails when a visit is scheduled, the day before, when the technician is on the way (with a tracking link) and when it is done. Turn on SMS or Email first."],
            Status = dispatch.NoticesEnabled ? S["Customer notices are on."] : S["Customer notices are off."],
            Done = dispatch.NoticesEnabled,
            Action = S["Dispatch Settings"],
            Url = Url.Action("Index", "Admin", new { area = "OrchardCore.Settings", groupId = DispatchConstants.SettingsGroupId }),
        });

        await AddAsync(model, features, DispatchConstants.Features.Mobile, S["Give technicians the Field page"], S["Their visits on the phone, offline, with checklists, parts, photos and the customer's signature, and their position while they work, if they agree."], () =>
            Task.FromResult((S["Workers linked to a user: {0}", linked].Value, linked > 0, S["Open the Field page"].Value, Url.Content("~/Admin/my-work/field"))));

        model.Steps.Add(new WorkforceSetupStep
        {
            Title = S["Dispatch the first visits"],
            Description = S["Each new work order gets a visit. Drag it onto a technician on the board, or let the optimizer propose the day."],
            Status = S["The board shows each technician's day, the visits waiting and a map."],
            Action = S["Dispatch Board"],
            Url = Url.Action("Index", "DispatchBoard"),
        });

        return View(model);
    }

    private async Task AddAsync(WorkforceSetupViewModel model, HashSet<string> features, string feature, string title, string description, Func<Task<(string Status, bool Done, string Action, string Url)>> check)
    {
        if (!features.Contains(feature))
        {
            model.Steps.Add(new WorkforceSetupStep { Title = title, Description = description, FeatureOff = true, Action = S["Features"], Url = Url.Content("~/Admin/Features") });

            return;
        }

        var (status, done, action, url) = await check();

        model.Steps.Add(new WorkforceSetupStep { Title = title, Description = description, Status = status, Done = done, Action = action, Url = url });
    }
}
