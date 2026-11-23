using System.Globalization;
using CrestApps.OrchardCore.Core.Controllers;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Indexes;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Migrations;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Billing;

/// <summary>
/// Registers billing: the charges kept to bill later with their admin pages and index, and the draft invoice's card that
/// adds unbilled work to it.
/// </summary>
[Feature(InvoicingConstants.Features.Billing)]
public sealed class BillingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddInvoicingBilling()
            .AddIndexProvider<BillableItemIndexProvider>()
            .AddDataMigration<BillableItemMigrations>()
            .AddDisplayDriver<BillableItem, BillableItemDisplayDriver>()
            .AddDisplayDriver<Invoice, InvoiceUnbilledWorkDisplayDriver>()
            .AddNavigationProvider<BillableItemsAdminMenu>();
    }
}

/// <summary>
/// With Time Tracker on: the time people tracked, billed at an hourly rate.
/// </summary>
[Feature(InvoicingConstants.Features.Billing)]
[RequireFeatures(TimeTrackerConstants.Features.TimeTracker)]
public sealed class TimeBillingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IBillableSource, TimeTrackerBillableSource>();
}

/// <summary>
/// Ended Time Tracker entries nobody billed yet, of one person in a period, at an hourly rate. The entry records the
/// invoice line that bills it, so there is no second record of the time.
/// </summary>
public sealed class TimeTrackerBillableSource : IBillableSource
{
    public const string SourceType = "time";

    private readonly ITimeTrackerService _tracker;
    private readonly ITimeStageManager _stages;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public TimeTrackerBillableSource(ITimeTrackerService tracker, ITimeStageManager stages, ILocalClock localClock, IStringLocalizer<TimeTrackerBillableSource> stringLocalizer)
    {
        _tracker = tracker;
        _stages = stages;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Type => SourceType;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BillableCandidate>> ListUnbilledAsync(BillableQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrEmpty(query.UserId) || query.HourlyRate is not > 0m)
        {
            return [];
        }

        var zone = await _localClock.GetLocalTimeZoneAsync();
        var fromUtc = query.From is { } from ? Calendars.CalendarTime.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone?.TimeZoneId) : (DateTime?)null;
        var toUtc = query.Through is { } through ? Calendars.CalendarTime.ToUtc(through.AddDays(1).ToDateTime(TimeOnly.MinValue), zone?.TimeZoneId) : (DateTime?)null;
        var entries = await _tracker.ListAsync(new TimeEntryQuery { UserIds = [query.UserId], FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken);
        var stages = (await _stages.GetAllAsync(cancellationToken)).ToDictionary(stage => stage.ItemId, stage => stage.Name, StringComparer.Ordinal);
        var candidates = new List<BillableCandidate>();

        foreach (var entry in entries.Where(entry => entry.EndUtc is not null && string.IsNullOrEmpty(entry.BilledInvoiceId)).OrderBy(entry => entry.StartUtc))
        {
            var hours = Math.Round((decimal)(entry.EndUtc.Value - entry.StartUtc).TotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);

            if (hours <= 0m)
            {
                continue;
            }

            var day = DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(entry.StartUtc, DateTimeKind.Utc))).DateTime);
            var stage = !string.IsNullOrEmpty(entry.StageId) && stages.TryGetValue(entry.StageId, out var name) ? name : S["Time"].Value;

            candidates.Add(new BillableCandidate
            {
                Source = SourceType,
                Id = entry.ItemId,
                Date = day,
                Kind = DocumentLineKind.Time,
                Description = string.IsNullOrWhiteSpace(entry.Note)
                    ? S["{0}, {1} on {2}", stage, entry.UserName, day.ToString("d", CultureInfo.CurrentCulture)].Value
                    : S["{0}, {1} on {2}: {3}", stage, entry.UserName, day.ToString("d", CultureInfo.CurrentCulture), entry.Note].Value,
                Quantity = hours,
                Unit = S["hours"].Value,
                UnitPrice = query.HourlyRate.Value,
                Currency = query.Currency,
            });
        }

        return candidates;
    }

    /// <inheritdoc/>
    public async Task<string> MarkBilledAsync(string id, string invoiceId, string lineId, CancellationToken cancellationToken = default)
    {
        var result = await _tracker.SetBilledAsync(id, invoiceId, lineId, cancellationToken);

        return result.Errors.Count == 0 ? null : string.Join(" ", result.Errors);
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(string id, CancellationToken cancellationToken = default)
        => _tracker.SetBilledAsync(id, null, null, cancellationToken);
}

/// <summary>
/// Adds Commerce > Unbilled Charges.
/// </summary>
public sealed class BillableItemsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public BillableItemsAdminMenu(IStringLocalizer<BillableItemsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], commerce => commerce
                .Add(S["Unbilled Charges"], S["Unbilled Charges"].PrefixPosition("4.6"), charges => charges
                    .Id("unbilledcharges")
                    .Action("Index", "BillableItems", new RouteValueDictionary { { "area", InvoicingConstants.Features.Area } })
                    .Permission(InvoicingPermissions.ManageInvoices)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Lists, adds, changes and deletes the charges kept to bill later.
/// </summary>
[Admin]
[Feature(InvoicingConstants.Features.Billing)]
public sealed class BillableItemsController : CatalogAdminController<BillableItem>
{
    public BillableItemsController(
        IBillableItemManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<BillableItem> displayManager,
        INotifier notifier,
        IHtmlLocalizer<BillableItemsController> htmlLocalizer,
        IStringLocalizer<BillableItemsController> stringLocalizer)
        : base(manager, authorizationService, updateModelAccessor, displayManager, notifier, htmlLocalizer, stringLocalizer)
    {
    }

    protected override Permission ManagePermission => InvoicingPermissions.ManageInvoices;

    protected override LocalizedString CreateDisplayName => S["Charge"];

    protected override LocalizedString NewDisplayName => S["New charge"];

    protected override LocalizedHtmlString CreatedNotification => H["The charge was added. It is offered on the customer's next draft invoice."];

    protected override LocalizedHtmlString UpdatedNotification => H["The charge was updated."];

    protected override LocalizedHtmlString DeletedNotification => H["The charge was deleted."];

    protected override string GetName(BillableItem entry) => entry.Description;

    protected override void SetName(BillableItem entry, string name) => entry.Description = name;

    // A charge an invoice bills is changed by taking it off the invoice first.
    protected override Task<bool> CanDeleteAsync(BillableItem model) => Task.FromResult(!model.IsBilled);

    [Admin("commerce/unbilled-charges", "BillableItemsIndex")]
    public Task<IActionResult> Index(CatalogEntryOptions options, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
        => IndexAsync(options, pagerParameters, pagerOptions, shapeFactory);

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("commerce/unbilled-charges", "BillableItemsIndex")]
    public Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
        => IndexFilterPostAsync(model, pagerParameters);

    [Admin("commerce/unbilled-charges/create", "BillableItemsCreate")]
    public Task<IActionResult> Create() => CreateAsync();

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("commerce/unbilled-charges/create", "BillableItemsCreate")]
    public Task<IActionResult> CreatePost() => CreatePostAsync();

    [Admin("commerce/unbilled-charges/edit/{id}", "BillableItemsEdit")]
    public Task<IActionResult> Edit(string id) => EditAsync(id);

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("commerce/unbilled-charges/edit/{id}", "BillableItemsEdit")]
    public Task<IActionResult> EditPost(string id) => EditPostAsync(id);

    [HttpPost]
    [Admin("commerce/unbilled-charges/delete/{id}", "BillableItemsDelete")]
    public Task<IActionResult> Delete(string id) => DeleteAsync(id);
}

/// <summary>
/// The row of a charge in its list, in both layouts, and its editor.
/// </summary>
public sealed class BillableItemDisplayDriver : DisplayDriver<BillableItem>
{
    private readonly IPartyDirectory _parties;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly LinkGenerator _links;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public BillableItemDisplayDriver(
        IPartyDirectory parties,
        ISiteService siteService,
        ILocalClock localClock,
        LinkGenerator links,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<BillableItemDisplayDriver> stringLocalizer)
    {
        _parties = parties;
        _siteService = siteService;
        _localClock = localClock;
        _links = links;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(BillableItem item, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{item.Description} {item.PartyName}".ToLowerInvariant();
        }

        return CombineAsync(
            View("BillableItem_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("BillableItem_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Charge:5"),
            View("BillableItem_Buttons_SummaryAdmin", item).Location("Actions:5"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(BillableItem item, BuildEditorContext context)
        => Initialize<BillableItemViewModel>("BillableItemFields_Edit", async model =>
        {
            var settings = await _siteService.GetSettingsAsync<InvoicingSettings>();

            model.Party = item.Party.IsEmpty ? null : item.Party.ToString();
            model.PartyName = item.PartyName ?? (item.Party.IsEmpty ? null : await _parties.GetNameAsync(item.Party));
            model.Kind = item.Kind;
            model.Date = item.Date == default ? DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime) : item.Date;
            model.Description = item.Description;
            model.Quantity = item.Quantity;
            model.Unit = item.Unit;
            model.UnitCost = item.UnitCost;
            model.MarkupPercent = item.MarkupPercent;
            model.Currency = item.Currency ?? settings.DefaultCurrency;
            model.TaxCategoryCode = item.TaxCategoryCode;
            model.IsBilled = item.IsBilled;
            model.InvoiceId = item.InvoiceId;
            model.PartySearchEndpoint = _links.GetPathByRouteValues(_httpContextAccessor.HttpContext, InvoicePartySearchEndpoints.RouteName, values: null);
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(BillableItem item, UpdateEditorContext context)
    {
        var model = new BillableItemViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (item.IsBilled)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Description), S["An invoice bills this charge, so it can't be changed. Take it off the invoice first."]);

            return Edit(item, context);
        }

        item.Party = PartyRef.TryParse(model.Party?.Trim(), out var party) ? party : default;
        item.PartyName = item.Party.IsEmpty ? null : await _parties.GetNameAsync(item.Party);
        item.Kind = model.Kind;
        item.Date = model.Date;
        item.Description = model.Description?.Trim();
        item.Quantity = model.Quantity;
        item.Unit = string.IsNullOrWhiteSpace(model.Unit) ? null : model.Unit.Trim();
        item.UnitCost = model.UnitCost;
        item.MarkupPercent = model.MarkupPercent;
        item.Currency = model.Currency?.Trim().ToUpperInvariant();
        item.TaxCategoryCode = string.IsNullOrWhiteSpace(model.TaxCategoryCode) ? null : model.TaxCategoryCode.Trim();

        return Edit(item, context);
    }
}

/// <summary>
/// The draft invoice's card that offers the customer's unbilled charges and, with Time Tracker, a person's unbilled time.
/// </summary>
public sealed class InvoiceUnbilledWorkDisplayDriver : DisplayDriver<Invoice>
{
    private readonly IBillingService _billing;
    private readonly IEnumerable<IBillableSource> _sources;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public InvoiceUnbilledWorkDisplayDriver(IBillingService billing, IEnumerable<IBillableSource> sources, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _billing = billing;
        _sources = sources;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(Invoice invoice, BuildDisplayContext context)
    {
        if (!string.Equals(context.DisplayType, InvoicingConstants.DisplayTypes.DetailAdmin, StringComparison.Ordinal) ||
            invoice.Status != InvoiceStatus.Draft ||
            invoice.Kind != InvoiceKind.Standard)
        {
            return null;
        }

        return Initialize<InvoiceUnbilledWorkViewModel>("InvoiceUnbilledWork_DetailAdmin", async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;

            model.Invoice = invoice;
            model.CanManage = await _authorizationService.AuthorizeAsync(httpContext?.User, InvoicingPermissions.ManageInvoices);
            model.HasParty = invoice.BillTo?.Party is { IsEmpty: false };

            if (model.HasParty)
            {
                model.Charges = await _billing.ListUnbilledAsync(ChargesBillableSource.SourceType, new BillableQuery { Party = invoice.BillTo.Party, Currency = invoice.Currency }, httpContext?.RequestAborted ?? default);
            }

            model.CanBillTime = _sources.Any(source => source.Type == TimeTrackerBillableSource.SourceType);

            if (model.CanBillTime && httpContext?.Request.Query is { } query && !string.IsNullOrEmpty(query["timeUser"]))
            {
                model.TimeUser = query["timeUser"];
                model.TimeFrom = DateOnly.TryParse(query["timeFrom"], CultureInfo.InvariantCulture, out var from) ? from : null;
                model.TimeThrough = DateOnly.TryParse(query["timeThrough"], CultureInfo.InvariantCulture, out var through) ? through : null;
                model.TimeRate = decimal.TryParse(query["timeRate"].ToString().Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate) ? rate : null;
                model.Time = await _billing.ListUnbilledAsync(TimeTrackerBillableSource.SourceType, model.TimeQuery, httpContext.RequestAborted);
            }
        }).Location(InvoicingConstants.DisplayTypes.DetailAdmin, "Content:6");
    }
}

/// <summary>
/// Adds unbilled charges or time to a draft invoice.
/// </summary>
[Admin("invoices/billing/{action}/{itemId}", "InvoiceBilling{action}")]
[Feature(InvoicingConstants.Features.Billing)]
public sealed class InvoiceBillingController : Controller
{
    private readonly IBillingService _billing;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public InvoiceBillingController(IBillingService billing, IAuthorizationService authorizationService, INotifier notifier, IHtmlLocalizer<InvoiceBillingController> htmlLocalizer)
    {
        _billing = billing;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Adds the chosen charges.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> AddCharges(string itemId, string[] ids)
        => AddAsync(itemId, ChargesBillableSource.SourceType, ids, new BillableQuery());

    /// <summary>
    /// Adds the chosen time of a person at an hourly rate.
    /// </summary>
    [HttpPost]
    public Task<IActionResult> AddTime(string itemId, string[] ids, string timeUser, DateOnly? timeFrom, DateOnly? timeThrough, decimal? timeRate)
        => AddAsync(itemId, TimeTrackerBillableSource.SourceType, ids, new BillableQuery { UserId = timeUser, From = timeFrom, Through = timeThrough, HourlyRate = timeRate });

    private async Task<IActionResult> AddAsync(string itemId, string source, string[] ids, BillableQuery query)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var result = await _billing.AddToInvoiceAsync(itemId, source, ids, query, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The work was added to the invoice."]);
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, "{0}", false, error));
            }
        }

        return RedirectToAction("Detail", "InvoicesAdmin", new { itemId });
    }
}
