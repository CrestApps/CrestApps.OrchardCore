using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using ISession = YesSql.ISession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// The My Work page where agents see how closely they kept to their schedule on each saved day.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Adherence)]
[RequireFeatures(WorkforceConstants.Features.Portal)]
public sealed class MyAdherenceController : Controller
{
    internal const int Days = 14;

    private readonly IAuthorizationService _authorizationService;
    private readonly MyWorkPortal _portal;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyAdherenceController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="portal">The My Work portal.</param>
    /// <param name="session">The session.</param>
    public MyAdherenceController(IAuthorizationService authorizationService, MyWorkPortal portal, ISession session)
    {
        _authorizationService = authorizationService;
        _portal = portal;
        _session = session;
    }

    /// <summary>
    /// Shows the worker's latest saved days.
    /// </summary>
    /// <returns>The page.</returns>
    [Admin("my-work/adherence")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return Forbid();
        }

        var worker = await _portal.GetWorkerAsync(User);

        if (worker is null)
        {
            return Redirect("~/Admin/my-work");
        }

        var workerId = worker.ItemId;
        var days = await _session.Query<AdherenceDay, AdherenceDayIndex>(index => index.WorkerId == workerId, collection: WorkforceConstants.CollectionName)
            .OrderByDescending(index => index.Date)
            .Take(Days * 3)
            .ListAsync(HttpContext.RequestAborted);

        // Days the worker wasn't scheduled say nothing about their adherence.
        List<AdherenceDay> scheduled = [.. days.Where(day => day.ScheduledSeconds > 0).Take(Days)];

        return View(new MyAdherenceViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "adherence", HttpContext.RequestAborted),
            Days = scheduled,
            List = new AdminListContext<MyAdherenceDayRow>
            {
                Source = scheduled.Select(day => new MyAdherenceDayRow { Day = day }).ToList(),
                ShowSelectAll = false,
            },
        });
    }
}

/// <summary>
/// The worker's adherence page.
/// </summary>
public class MyAdherenceViewModel
{
    /// <summary>
    /// Gets or sets the portal's menu.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the latest saved days, newest first.
    /// </summary>
    public IList<AdherenceDay> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of the latest saved days.
    /// </summary>
    public AdminListContext List { get; set; }
}

/// <summary>
/// Adds Adherence to My Work for workers who have saved adherence days.
/// </summary>
internal sealed class MyAdherenceLinkProvider : IMyWorkLinkProvider
{
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public MyAdherenceLinkProvider(ISession session, IStringLocalizer<MyAdherenceLinkProvider> stringLocalizer)
    {
        _session = session;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<MyWorkLink>> GetLinksAsync(ClaimsPrincipal user, StaffMember worker, CancellationToken cancellationToken = default)
    {
        if (worker is null)
        {
            return [];
        }

        var workerId = worker.ItemId;
        var any = await _session.QueryIndex<AdherenceDayIndex>(index => index.WorkerId == workerId, collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

        return any is null ? [] : [new MyWorkLink("adherence", S["Adherence"], "~/Admin/my-work/adherence", 0)];
    }
}
