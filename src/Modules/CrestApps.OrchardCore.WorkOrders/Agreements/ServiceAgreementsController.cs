using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.WorkOrders.Agreements.Drivers;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// An agreement's visits: its next visits and the work orders it made, and making the due ones now. The agreement's list
/// and editor are the Agreements module's.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
public sealed class ServiceAgreementsController : Controller
{
    private const int NextVisits = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly IAgreementManager _agreements;
    private readonly ServiceAgreementScheduler _scheduler;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementsController"/> class.
    /// </summary>
    public ServiceAgreementsController(
        IAuthorizationService authorizationService,
        IAgreementManager agreements,
        ServiceAgreementScheduler scheduler,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        ISession session,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<ServiceAgreementsController> htmlLocalizer,
        IStringLocalizer<ServiceAgreementsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _agreements = agreements;
        _scheduler = scheduler;
        _types = types;
        _lookups = lookups;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// The agreements, which the Agreements module lists.
    /// </summary>
    /// <returns>A redirect to the list.</returns>
    [Admin("work-orders/agreements", "ServiceAgreements")]
    public IActionResult Index()
        => RedirectToAction("Index", "Agreements", new { area = AgreementsConstants.Features.Agreements });

    /// <summary>
    /// Shows an agreement's visits and work orders.
    /// </summary>
    /// <param name="id">The agreement.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/agreements/view/{id}", "ServiceAgreementsView")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AgreementsPermissions.ViewAgreements))
        {
            return Forbid();
        }

        var agreement = await _agreements.FindByIdAsync(id);

        if (agreement is null)
        {
            return NotFound();
        }

        var terms = ServiceAgreementEditorRules.TermsOf(agreement);
        var types = (await _types.GetAllAsync()).ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var workOrders = await ServiceAgreementEditorRules.WorkOrdersAsync(_session, agreement.ItemId, 50, HttpContext.RequestAborted);
        var assets = terms.AssetIds.Count == 0 ? [] : await _lookups.AssetsAsync(HttpContext.RequestAborted);
        var presets = WorkOrderAgreementTermsDisplayDriver.Presets(S);

        return View(new ServiceAgreementDetailsViewModel
        {
            Agreement = agreement,
            Terms = terms,
            Schedules = [.. terms.Schedules.Select(schedule => new ScheduleVisits
            {
                Schedule = schedule,
                TypeName = types.GetValueOrDefault(schedule.TypeId ?? string.Empty),
                Repeats = presets.TryGetValue(schedule.Rule ?? string.Empty, out var words) ? words : schedule.Rule,
                Next = [.. ServiceAgreementEditorRules.NextVisits(agreement, schedule, _clock.UtcNow, NextVisits)],
            })],
            WorkOrders = [.. workOrders],
            CustomerNames = await HttpContext.RequestServices.GetRequiredService<Integrations.WorkOrderCustomers>().NamesAsync(terms.CustomerId, agreement.AccountId, null, pickers: false, HttpContext.RequestAborted),
            Assets = [.. assets.Where(asset => terms.AssetIds.Contains(asset.Value))],
        });
    }

    /// <summary>
    /// Makes the agreement's due work orders now, instead of waiting for the hourly task.
    /// </summary>
    /// <param name="id">The agreement.</param>
    /// <returns>Its page.</returns>
    [HttpPost]
    [Admin("work-orders/agreements/view/{id}/run", "ServiceAgreementsRun")]
    public async Task<IActionResult> Run(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AgreementsPermissions.ManageAgreements))
        {
            return Forbid();
        }

        var agreement = await _agreements.FindByIdAsync(id);

        if (agreement is null)
        {
            return NotFound();
        }

        var made = await _scheduler.RunAsync(agreement, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(made.Count switch
        {
            0 => H["No work orders were due."],
            1 => H["1 work order was made."],
            _ => H["{0} work orders were made.", made.Count],
        });

        return RedirectToAction(nameof(Details), new { id });
    }
}
