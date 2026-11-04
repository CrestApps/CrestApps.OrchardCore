using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The visits list and each visit's page, where a visit is assigned, moved and moved along.
/// </summary>
public sealed class VisitsController : Controller
{
    /// <summary>
    /// The status filter for visits still to happen or happening.
    /// </summary>
    public const string OpenFilter = "open";

    /// <summary>
    /// The status filter for every visit.
    /// </summary>
    public const string AllFilter = "all";

    private readonly IAuthorizationService _authorizationService;
    private readonly IDispatchService _dispatch;
    private readonly IDispatchCandidateFinder _finder;
    private readonly DispatchResources _resources;
    private readonly WorkOrderStore _workOrders;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly DispatchTimeZone _timeZone;
    private readonly IGeoSettingsService _geoSettings;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisitsController"/> class.
    /// </summary>
    public VisitsController(
        IAuthorizationService authorizationService,
        IDispatchService dispatch,
        IDispatchCandidateFinder finder,
        DispatchResources resources,
        WorkOrderStore workOrders,
        WorkOrderSettingManager<WorkOrderType> types,
        DispatchTimeZone timeZone,
        IGeoSettingsService geoSettings,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<VisitsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _dispatch = dispatch;
        _finder = finder;
        _resources = resources;
        _workOrders = workOrders;
        _types = types;
        _timeZone = timeZone;
        _geoSettings = geoSettings;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists visits.
    /// </summary>
    [Admin("dispatch/visits", "DispatchVisits")]
    public async Task<IActionResult> Index(VisitsListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ViewDispatch))
        {
            return Forbid();
        }

        form.Status ??= OpenFilter;

        var zone = await _timeZone.GetAsync();
        var workers = await _resources.ListWorkersAsync(HttpContext.RequestAborted);
        var names = workers.ToDictionary(worker => worker.ItemId, worker => worker.DisplayName, StringComparer.Ordinal);
        var query = _session.Query<ServiceVisit, ServiceVisitIndex>(collection: DispatchConstants.CollectionName);

        if (form.Status == OpenFilter)
        {
            query = query.Where(index => index.IsOpen);
        }
        else if (Enum.TryParse<VisitStatus>(form.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            var q = form.Q.Trim();

            query = query.Where(index => index.Number.Contains(q) || index.WorkOrderNumber.Contains(q) || index.Summary.Contains(q));
        }

        if (DateOnly.TryParseExact(form.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var from = CalendarTime.ToUtc(day.ToDateTime(TimeOnly.MinValue), zone);
            var to = CalendarTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

            query = query.Where(index => index.StartUtc < to && index.EndUtc > from);
        }

        if (!string.IsNullOrEmpty(form.WorkerId))
        {
            var ids = (await _session.QueryIndex<VisitWorkerIndex>(index => index.WorkerId == form.WorkerId, collection: DispatchConstants.CollectionName).ListAsync(HttpContext.RequestAborted))
                .Select(index => index.VisitId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            query = query.Where(index => index.ItemId.IsIn(ids));
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var count = await query.CountAsync(HttpContext.RequestAborted);
        var page = await query.OrderByDescending(index => index.Priority).ThenBy(index => index.StartUtc).ThenByDescending(index => index.CreatedUtc).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync(HttpContext.RequestAborted);
        var types = (await _types.GetAllAsync()).ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var now = _clock.UtcNow;

        ViewData["TimeZoneId"] = zone;
        form.WithoutVisits = (await _dispatch.ListWorkOrdersWithoutVisitsAsync(HttpContext.RequestAborted)).Count;
        form.WorkerOptions = [.. workers.OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, worker.ItemId == form.WorkerId))];
        form.List = new AdminListContext<VisitRow>
        {
            Source = page.Select(visit => new VisitRow
            {
                Visit = visit,
                Workers = string.Join(", ", visit.WorkerIds.Select(id => names.GetValueOrDefault(id, id))),
                TypeName = types.GetValueOrDefault(visit.TypeId ?? string.Empty),
                NowUtc = now,
            }).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count),
        };

        return View(form);
    }

    /// <summary>
    /// Makes another visit for a work order.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/work-orders/{workOrderId}/visits", "DispatchVisitsCreate")]
    public async Task<IActionResult> Create(string workOrderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var workOrder = await _workOrders.FindByIdAsync(workOrderId, HttpContext.RequestAborted);

        if (workOrder is null)
        {
            return NotFound();
        }

        var result = await _dispatch.CreateAsync(workOrder, User.Identity?.Name, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await NotifyAsync(result, null);

            return Redirect(Url.Action("Details", "WorkOrders", new { area = "CrestApps.OrchardCore.WorkOrders", id = workOrderId }));
        }

        return RedirectToAction(nameof(Details), new { id = result.Visit.ItemId });
    }

    /// <summary>
    /// Makes a visit for every open work order without one.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/make-missing-visits", "DispatchVisitsMakeMissing")]
    public async Task<IActionResult> MakeMissing()
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var made = await _dispatch.CreateMissingAsync(User.Identity?.Name, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["{0} visits were made.", made]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a visit, and the best candidates for it on a day when asked.
    /// </summary>
    [Admin("dispatch/visits/{id}", "DispatchVisit")]
    public async Task<IActionResult> Details(string id, string day, bool find = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ViewDispatch))
        {
            return Forbid();
        }

        var visit = await _dispatch.FindAsync(id, HttpContext.RequestAborted);

        if (visit is null)
        {
            return NotFound();
        }

        var zone = await _timeZone.GetAsync();
        var model = await BuildDetailsAsync(visit, zone, day);

        if (find && model.CanManage && DateOnly.TryParseExact(model.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            var from = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);

            model.Candidates = await _finder.FindAsync(visit, from, from.AddDays(1), null, HttpContext.RequestAborted);
        }

        return View(model);
    }

    /// <summary>
    /// Assigns a visit to workers at a time.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/visits/{id}/assign", "DispatchVisitAssign")]
    public async Task<IActionResult> Assign(string id, string[] workerIds, string start, string startUtc, string day)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var zone = await _timeZone.GetAsync();
        DateTime when;

        if (DateTime.TryParse(startUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
        {
            when = utc;
        }
        else if (DateTime.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            when = CalendarTime.ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        }
        else
        {
            await _notifier.ErrorAsync(H["Choose when the visit starts."]);

            return RedirectToAction(nameof(Details), new { id, day });
        }

        var result = await _dispatch.AssignAsync(id, new VisitAssignment { WorkerIds = workerIds ?? [], StartUtc = when }, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["{0} is scheduled.", result.Visit?.Number ?? string.Empty]);

        return RedirectToAction(nameof(Details), new { id, day });
    }

    /// <summary>
    /// Takes a visit off its workers.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/visits/{id}/unassign", "DispatchVisitUnassign")]
    public async Task<IActionResult> Unassign(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var result = await _dispatch.UnassignAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["{0} is back with the unscheduled visits.", result.Visit?.Number ?? string.Empty]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Moves a visit to another status.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/visits/{id}/status", "DispatchVisitStatus")]
    public async Task<IActionResult> Status(string id, VisitStatus status, string note)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var result = await _dispatch.ChangeStatusAsync(id, status, note, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, H["{0} is {1}.", result.Visit?.Number ?? string.Empty, status.Text().ToLowerInvariant()]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Pins a visit where it is, or lets it move again.
    /// </summary>
    [HttpPost]
    [Admin("dispatch/visits/{id}/pin", "DispatchVisitPin")]
    public async Task<IActionResult> Pin(string id, bool pinned)
    {
        if (!await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch))
        {
            return Forbid();
        }

        var result = await _dispatch.PinAsync(id, pinned, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result, pinned ? H["Pinned."] : H["Unpinned."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<VisitDetailsViewModel> BuildDetailsAsync(ServiceVisit visit, string zone, string day)
    {
        var workOrder = await _workOrders.FindByIdAsync(visit.WorkOrderId, HttpContext.RequestAborted);
        var workers = await _resources.ListWorkersAsync(HttpContext.RequestAborted);
        var type = string.IsNullOrEmpty(visit.TypeId) ? null : await _types.FindByIdAsync(visit.TypeId, HttpContext.RequestAborted);
        var today = DateOnly.FromDateTime(CalendarTime.ToLocal(_clock.UtcNow, zone));
        var shownDay = DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : visit.StartUtc is { } start ? DateOnly.FromDateTime(CalendarTime.ToLocal(start, zone))
            : workOrder?.EarliestUtc is { } earliest && DateOnly.FromDateTime(CalendarTime.ToLocal(earliest, zone)) > today ? DateOnly.FromDateTime(CalendarTime.ToLocal(earliest, zone))
            : today;
        var model = new VisitDetailsViewModel
        {
            Visit = visit,
            WorkOrder = workOrder,
            TypeName = type?.Name,
            TimeZoneId = zone,
            Day = shownDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CanManage = await _authorizationService.AuthorizeAsync(User, DispatchPermissions.ManageDispatch),
            NextStatuses = [.. VisitStatusRules.Next(visit.Status)],
            OtherVisits = [.. (await _dispatch.ListForWorkOrderAsync(visit.WorkOrderId, HttpContext.RequestAborted)).Where(other => other.ItemId != visit.ItemId)],
        };

        foreach (var worker in workers)
        {
            model.WorkerNames[worker.ItemId] = worker.DisplayName;
        }

        foreach (var workerId in visit.WorkerIds.Where(workerId => !model.WorkerNames.ContainsKey(workerId)))
        {
            model.WorkerNames[workerId] = (await _resources.FindWorkerAsync(workerId, HttpContext.RequestAborted))?.DisplayName ?? workerId;
        }

        model.WorkerOptions = [.. workers.OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(worker => new SelectListItem(worker.DisplayName, worker.ItemId, visit.WorkerIds.Contains(worker.ItemId)))];

        foreach (var assetId in visit.EquipmentAssetIds)
        {
            model.EquipmentNames[assetId] = (await _session.QueryIndex<Assets.Core.Indexes.AssetIndex>(index => index.ItemId == assetId, collection: Assets.AssetsConstants.CollectionName).FirstOrDefaultAsync(HttpContext.RequestAborted))?.Name ?? assetId;
        }

        if (VisitStatusRules.IsOpen(visit.Status) && visit.StartUtc is not null && HttpContext.RequestServices.GetService(typeof(IVisitTrackingLinks)) is IVisitTrackingLinks links && HttpContext.RequestServices.GetService(typeof(IDispatchSiteInfo)) is IDispatchSiteInfo site)
        {
            model.TrackingUrl = await site.TrackingUrlAsync(links.CreateToken(visit));
        }

        if (GeoPoint.From(workOrder?.ServiceAddress?.Latitude, workOrder?.ServiceAddress?.Longitude) is not null)
        {
            model.Map = await _geoSettings.GetAsync();
        }

        return model;
    }

    private async Task NotifyAsync(DispatchResult result, LocalizedHtmlString success)
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

/// <summary>
/// Gives the time zone dispatch shows times in: the dispatch settings' or the site's.
/// </summary>
public sealed class DispatchTimeZone
{
    private readonly IDispatchSettingsProvider _settings;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchTimeZone"/> class.
    /// </summary>
    public DispatchTimeZone(IDispatchSettingsProvider settings, ISiteService siteService)
    {
        _settings = settings;
        _siteService = siteService;
    }

    /// <summary>
    /// Gets the time zone.
    /// </summary>
    /// <returns>The time zone's id.</returns>
    public async Task<string> GetAsync()
    {
        var zone = (await _settings.GetAsync()).TimeZoneId;

        if (string.IsNullOrEmpty(zone))
        {
            zone = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;
        }

        return CalendarTime.IsValidZone(zone) ? zone : "UTC";
    }

    /// <summary>
    /// Formats a time in a zone.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="zone">The zone.</param>
    /// <param name="format">The format.</param>
    /// <returns>The text.</returns>
    public static string Format(DateTime? utc, string zone, string format = "ddd d MMM, HH:mm")
        => utc is null ? null : CalendarTime.ToLocal(utc.Value, zone).ToString(format, CultureInfo.CurrentCulture);
}
