using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Dispatch.Controllers;

/// <summary>
/// The customer's page for their visit: when to expect the technician, and who. It needs no sign-in; the signed link
/// is the key, and it stops working once the visit is over.
/// </summary>
[AllowAnonymous]
public sealed class TrackingController : Controller
{
    private readonly IVisitTrackingLinks _links;
    private readonly DispatchResources _resources;
    private readonly DispatchTimeZone _timeZone;
    private readonly IDispatchSettingsProvider _settings;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingController"/> class.
    /// </summary>
    public TrackingController(IVisitTrackingLinks links, DispatchResources resources, DispatchTimeZone timeZone, IDispatchSettingsProvider settings, ISiteService siteService, IStringLocalizer<TrackingController> stringLocalizer)
    {
        _links = links;
        _resources = resources;
        _timeZone = timeZone;
        _settings = settings;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the visit, or says the link has expired.
    /// </summary>
    [HttpGet("track/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        var model = new VisitTrackingViewModel { Company = (await _siteService.GetSiteSettingsAsync()).SiteName };

        if (!(await _settings.GetAsync()).TrackingEnabled || await _links.ReadAsync(token, HttpContext.RequestAborted) is not { } visit)
        {
            model.Expired = true;
            Response.StatusCode = StatusCodes.Status404NotFound;

            return View(model);
        }

        var zone = await _timeZone.GetAsync();
        var lead = await _resources.FindWorkerAsync(visit.LeadWorkerId, HttpContext.RequestAborted);

        model.Number = visit.Number;
        model.Status = visit.Status switch
        {
            VisitStatus.Unscheduled => S["Being scheduled"],
            VisitStatus.Scheduled or VisitStatus.Dispatched => S["Scheduled"],
            VisitStatus.EnRoute => S["On the way"],
            VisitStatus.OnSite => S["Arrived"],
            _ => S["Paused"],
        };
        model.EnRoute = visit.Status == VisitStatus.EnRoute;
        model.Technician = VisitNoticeService.FirstName(lead?.DisplayName);
        model.Day = DispatchTimeZone.Format(visit.StartUtc, zone, "dddd d MMMM");
        model.Window = visit.ArrivalStartUtc is null ? null : $"{DispatchTimeZone.Format(visit.ArrivalStartUtc, zone, "HH:mm")}–{DispatchTimeZone.Format(visit.ArrivalEndUtc, zone, "HH:mm")}";
        model.Eta = visit.Status == VisitStatus.EnRoute ? DispatchTimeZone.Format(visit.EtaUtc, zone, "HH:mm") : null;

        return View(model);
    }
}
