using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using NodaTime;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;
using YesSql;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// Service agreements: the list, the editor and an agreement's page with its next visits and work orders.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
public sealed class ServiceAgreementsController : Controller
{
    private const int NextVisits = 3;

    private readonly IAuthorizationService _authorizationService;
    private readonly ServiceAgreementManager _agreements;
    private readonly ServiceAgreementScheduler _scheduler;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly WorkOrderLookups _lookups;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agreements">The agreements.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="types">The work order types.</param>
    /// <param name="lookups">The lookups.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceAgreementsController(
        IAuthorizationService authorizationService,
        ServiceAgreementManager agreements,
        ServiceAgreementScheduler scheduler,
        WorkOrderSettingManager<WorkOrderType> types,
        WorkOrderLookups lookups,
        ISession session,
        IClock clock,
        ISiteService siteService,
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
        _siteService = siteService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the agreements, searched by number or name, by status.
    /// </summary>
    /// <param name="form">The search and filter.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/agreements", "ServiceAgreements")]
    public async Task<IActionResult> Index(ServiceAgreementsListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return Forbid();
        }

        var query = _session.Query<ServiceAgreement, ServiceAgreementIndex>(collection: WorkOrdersConstants.CollectionName);

        if (Enum.TryParse<ServiceAgreementStatus>(form.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            var q = form.Q.Trim();

            query = query.Where(index => index.Number.Contains(q) || index.Name.Contains(q) || index.CustomerId == q || index.CompanyId == q);
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var count = await query.CountAsync(HttpContext.RequestAborted);
        var page = await query.OrderBy(index => index.Name).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync(HttpContext.RequestAborted);

        form.List = new AdminListContext<ServiceAgreementRow>
        {
            Source = [.. page.Select(agreement => new ServiceAgreementRow { Agreement = agreement })],
            Pager = await shapeFactory.PagerAsync(pager, count),
        };

        return View(form);
    }

    /// <summary>
    /// Shows an agreement with its next visits and the work orders it made.
    /// </summary>
    /// <param name="id">The agreement.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/agreements/view/{id}", "ServiceAgreementsView")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return Forbid();
        }

        var agreement = await _agreements.FindByIdAsync(id);

        if (agreement is null)
        {
            return NotFound();
        }

        var zone = ServiceAgreementScheduler.ZoneOf(agreement.TimeZoneId);
        var today = Instant.FromDateTimeUtc(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).InZone(zone).Date;
        var from = LocalDate.Max(today, LocalDate.FromDateOnly(agreement.StartDate));
        var types = (await _types.GetAllAsync()).ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var prefix = agreement.ItemId + ":";
        var source = nameof(WorkOrderSource.Agreement);
        var workOrders = await _session.Query<WorkOrder, WorkOrderIndex>(index => index.Source == source && index.SourceId.StartsWith(prefix), collection: WorkOrdersConstants.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(50)
            .ListAsync(HttpContext.RequestAborted);
        var assets = agreement.AssetIds.Count == 0 ? [] : await _lookups.AssetsAsync(HttpContext.RequestAborted);

        return View(new ServiceAgreementDetailsViewModel
        {
            Agreement = agreement,
            Schedules = [.. agreement.Schedules.Select(schedule => new ScheduleVisits
            {
                Schedule = schedule,
                TypeName = types.GetValueOrDefault(schedule.TypeId ?? string.Empty),
                Repeats = Describe(schedule.Rule),
                Next = [.. ServiceAgreementScheduler.Occurrences(agreement, schedule, zone, from, from.PlusYears(5)).Take(NextVisits).Select(date => date.ToDateOnly())],
            })],
            WorkOrders = [.. workOrders],
            Assets = [.. assets.Where(asset => agreement.AssetIds.Contains(asset.Value))],
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
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
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

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The agreement, or <see langword="null"/> for a new one.</param>
    /// <returns>The page.</returns>
    [Admin("work-orders/agreements/edit/{id?}", "ServiceAgreementsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return Forbid();
        }

        var agreement = string.IsNullOrEmpty(id) ? await _agreements.NewAsync() : await _agreements.FindByIdAsync(id);

        if (agreement is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(id))
        {
            agreement.TimeZoneId = (await _siteService.GetSiteSettingsAsync()).TimeZoneId;
        }

        var address = agreement.ServiceAddress;
        var model = new ServiceAgreementEditViewModel
        {
            Id = id,
            Number = agreement.Number,
            Name = agreement.Name,
            Status = agreement.Status,
            CustomerId = agreement.CustomerId,
            CompanyId = agreement.CompanyId,
            ContactName = agreement.Contact?.Name,
            ContactPhone = agreement.Contact?.Phone,
            ContactEmail = agreement.Contact?.Email,
            AddressLine1 = address?.AddressLine1,
            City = address?.City,
            Region = address?.Region,
            PostalCode = address?.PostalCode,
            Country = address?.Country,
            AccessNotes = agreement.AccessNotes,
            AssetIds = [.. agreement.AssetIds],
            StartDate = agreement.StartDate,
            EndDate = agreement.EndDate,
            TimeZoneId = agreement.TimeZoneId,
            Pricing = agreement.Pricing,
            PricePerVisit = agreement.PricePerVisit,
            ExpiringNoticeDays = agreement.ExpiringNoticeDays,
            Notes = agreement.Notes,
            Schedules = [.. agreement.Schedules.Select(schedule => new ScheduleEditModel
            {
                Id = schedule.Id,
                Name = schedule.Name,
                TypeId = schedule.TypeId,
                Rule = Presets.ContainsKey(schedule.Rule ?? string.Empty) ? schedule.Rule : string.Empty,
                CustomRule = Presets.ContainsKey(schedule.Rule ?? string.Empty) ? null : schedule.Rule,
                LeadDays = schedule.LeadDays,
            })],
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The agreement's page, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("work-orders/agreements/edit/{id?}", "ServiceAgreementsEdit")]
    public async Task<IActionResult> EditPost(ServiceAgreementEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var agreement = isNew ? await _agreements.NewAsync() : await _agreements.FindByIdAsync(form.Id);

        if (agreement is null)
        {
            return NotFound();
        }

        var addressChanged = agreement.ServiceAddress is not { } current
            || current.AddressLine1 != Clean(form.AddressLine1)
            || current.City != Clean(form.City)
            || current.Region != Clean(form.Region)
            || current.PostalCode != Clean(form.PostalCode)
            || current.Country != Clean(form.Country);

        // A new end date, or a pause, gives the agreement a new notice.
        var renotify = agreement.EndDate != form.EndDate || form.Status != ServiceAgreementStatus.Active;

        agreement.Name = form.Name?.Trim();
        agreement.Status = form.Status;
        agreement.CustomerId = Clean(form.CustomerId);
        agreement.CompanyId = Clean(form.CompanyId);
        agreement.Contact = new WorkOrderContact { Name = Clean(form.ContactName), Phone = Clean(form.ContactPhone), Email = Clean(form.ContactEmail) };
        agreement.AccessNotes = Clean(form.AccessNotes);
        agreement.AssetIds = [.. (form.AssetIds ?? []).Where(assetId => !string.IsNullOrEmpty(assetId)).Distinct(StringComparer.Ordinal)];
        agreement.StartDate = form.StartDate;
        agreement.EndDate = form.EndDate;
        agreement.TimeZoneId = Clean(form.TimeZoneId);
        agreement.Pricing = form.Pricing;
        agreement.PricePerVisit = form.Pricing == AgreementPricing.PerVisit ? form.PricePerVisit : 0;
        agreement.ExpiringNoticeDays = form.ExpiringNoticeDays;
        agreement.Notes = Clean(form.Notes);

        if (renotify)
        {
            agreement.ExpiringNotifiedUtc = null;
        }

        if (addressChanged)
        {
            agreement.ServiceAddress = Clean(form.AddressLine1) is null && Clean(form.City) is null && Clean(form.PostalCode) is null
                ? null
                : new Address { AddressLine1 = Clean(form.AddressLine1), City = Clean(form.City), Region = Clean(form.Region), PostalCode = Clean(form.PostalCode), Country = Clean(form.Country) };
        }

        var previous = agreement.Schedules.Where(schedule => !string.IsNullOrEmpty(schedule.Id)).ToDictionary(schedule => schedule.Id, StringComparer.Ordinal);

        agreement.Schedules = [.. (form.Schedules ?? []).Where(row => !string.IsNullOrEmpty(row.TypeId) || !string.IsNullOrWhiteSpace(row.Name)).Select(row => new AgreementSchedule
        {
            Id = string.IsNullOrEmpty(row.Id) ? CrestApps.Core.UniqueId.GenerateId() : row.Id,
            Name = Clean(row.Name),
            TypeId = row.TypeId,
            Rule = string.IsNullOrEmpty(row.Rule) ? Clean(row.CustomRule) : row.Rule,
            LeadDays = row.LeadDays,
            AssetIds = !string.IsNullOrEmpty(row.Id) && previous.TryGetValue(row.Id, out var old) ? old.AssetIds : [],
        })];

        var validation = await _agreements.ValidateAsync(agreement);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            form.Number = agreement.Number;
            await PopulateAsync(form);

            return View(form);
        }

        if (isNew)
        {
            await _agreements.CreateAsync(agreement);
        }
        else
        {
            await _agreements.UpdateAsync(agreement);
        }

        await _notifier.SuccessAsync(H["Service agreement {0} was saved.", agreement.Number]);

        return RedirectToAction(nameof(Details), new { id = agreement.ItemId });
    }

    /// <summary>
    /// Deletes a draft agreement; one that was in force is canceled instead.
    /// </summary>
    /// <param name="id">The agreement.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("work-orders/agreements/delete/{id}", "ServiceAgreementsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return Forbid();
        }

        var agreement = await _agreements.FindByIdAsync(id);

        if (agreement is null)
        {
            return NotFound();
        }

        if (agreement.Status != ServiceAgreementStatus.Draft)
        {
            await _notifier.WarningAsync(H["Only a draft agreement can be deleted. Cancel {0} instead.", agreement.Number]);

            return RedirectToAction(nameof(Index));
        }

        await _agreements.DeleteAsync(agreement);
        await _notifier.SuccessAsync(H["The service agreement was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private Dictionary<string, string> Presets => new(StringComparer.Ordinal)
    {
        ["FREQ=WEEKLY"] = S["Every week"],
        ["FREQ=WEEKLY;INTERVAL=2"] = S["Every 2 weeks"],
        ["FREQ=MONTHLY"] = S["Every month"],
        ["FREQ=MONTHLY;INTERVAL=3"] = S["Every 3 months"],
        ["FREQ=MONTHLY;INTERVAL=6"] = S["Every 6 months"],
        ["FREQ=YEARLY"] = S["Every year"],
    };

    private string Describe(string rule)
        => Presets.TryGetValue(rule ?? string.Empty, out var words) ? words : rule;

    private async Task PopulateAsync(ServiceAgreementEditViewModel model)
    {
        model.TypeOptions = [.. (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new SelectListItem(type.Name, type.ItemId))];
        model.AssetOptions = [.. (await _lookups.AssetsAsync(HttpContext.RequestAborted)).Select(option => new SelectListItem(option.Text, option.Value, model.AssetIds.Contains(option.Value)))];
        model.TimeZoneOptions = [.. _clock.GetTimeZones().Select(zone => zone.TimeZoneId).Order(StringComparer.Ordinal).Select(zoneId => new SelectListItem(zoneId, zoneId, string.Equals(zoneId, model.TimeZoneId, StringComparison.OrdinalIgnoreCase)))];
        model.RuleOptions = [.. Presets.Select(preset => new SelectListItem(preset.Value, preset.Key)), new SelectListItem(S["Custom rule"], string.Empty)];

        // One empty row to add a schedule.
        if (!model.Schedules.Any(schedule => string.IsNullOrEmpty(schedule.TypeId) && string.IsNullOrWhiteSpace(schedule.Name)))
        {
            model.Schedules.Add(new ScheduleEditModel { Rule = "FREQ=MONTHLY;INTERVAL=6" });
        }
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
