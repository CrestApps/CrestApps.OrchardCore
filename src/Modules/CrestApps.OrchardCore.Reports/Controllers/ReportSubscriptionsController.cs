using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Reports.Controllers;

/// <summary>
/// Reports › Subscriptions: reports emailed on a schedule, such as the weekly sales report.
/// </summary>
[Feature(ReportsConstants.SubscriptionsFeature)]
[Admin("Reports/subscriptions/{action}/{itemId?}", "ReportSubscriptions{action}")]
public sealed class ReportSubscriptionsController : Controller
{
    private readonly IReportSubscriptionStore _store;
    private readonly ReportSubscriptionService _service;
    private readonly ReportSubscriptionEditor _editor;
    private readonly IReportManager _reports;
    private readonly IReportExportManager _exports;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;

    public ReportSubscriptionsController(
        IReportSubscriptionStore store,
        ReportSubscriptionService service,
        ReportSubscriptionEditor editor,
        IReportManager reports,
        IReportExportManager exports,
        IStringLocalizerFactory localizerFactory,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<ReportSubscriptionsController> htmlLocalizer)
    {
        _store = store;
        _service = service;
        _editor = editor;
        _reports = reports;
        _exports = exports;
        _localizerFactory = localizerFactory;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
    }

    [Admin("Reports/subscriptions", "ReportSubscriptions")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, subscriptions) = await _store.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        return View(new ReportSubscriptionsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<ReportSubscription>
            {
                Source = subscriptions,
                Pager = (IShape)await shapeFactory.PagerAsync(pager, count, routeData),
            },
        });
    }

    public async Task<IActionResult> Create(string report)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        return View("Edit", await PopulateAsync(new ReportSubscriptionEditViewModel { ReportName = report, Recipients = null }, subscription: null));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(ReportSubscriptionEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        var subscription = new ReportSubscription { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow, CreatedBy = User.Identity?.Name };

        if (!await ApplyAsync(model, subscription))
        {
            return View("Edit", await PopulateAsync(model, subscription: null));
        }

        await _service.ScheduleAsync(subscription);
        await _store.CreateAsync(subscription);

        return await AfterSaveAsync(subscription, submit);
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        if (await _store.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new ReportSubscriptionEditViewModel
        {
            Name = subscription.Name,
            ReportName = subscription.ReportName,
            Frequency = subscription.Frequency,
            DayOfWeek = subscription.DayOfWeek,
            DayOfMonth = subscription.DayOfMonth,
            Hour = subscription.Hour,
            Recipients = subscription.Recipients,
            AttachmentFormat = subscription.AttachmentFormat,
            IsEnabled = subscription.IsEnabled,
        }, subscription));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, ReportSubscriptionEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        if (await _store.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        if (!await ApplyAsync(model, subscription))
        {
            return View(await PopulateAsync(model, subscription));
        }

        await _service.ScheduleAsync(subscription);
        subscription.ModifiedUtc = _clock.UtcNow;
        subscription.ModifiedBy = User.Identity?.Name;
        await _store.UpdateAsync(subscription);

        return await AfterSaveAsync(subscription, submit);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReportSubscriptionPermissions.ManageReportSubscriptions))
        {
            return Forbid();
        }

        if (await _store.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        await _store.DeleteAsync(subscription);
        await _notifier.SuccessAsync(H["The subscription was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    // "send" emails the report now, for the period before now, without moving the schedule.
    private async Task<IActionResult> AfterSaveAsync(ReportSubscription subscription, string submit)
    {
        if (submit == "send")
        {
            if (await _service.SendAsync(subscription, _clock.UtcNow, reschedule: false, HttpContext.RequestAborted))
            {
                await _notifier.SuccessAsync(H["The report was emailed to {0}.", subscription.Recipients]);
            }
            else
            {
                await _notifier.ErrorAsync(H["The report wasn't emailed: {0}", subscription.LastError]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(H["The subscription was saved."]);
        }

        return RedirectToAction(nameof(Edit), new { itemId = subscription.ItemId });
    }

    private async Task<bool> ApplyAsync(ReportSubscriptionEditViewModel model, ReportSubscription subscription)
    {
        var errors = await _editor.ApplyAsync(User, new ReportSubscriptionInput
        {
            Name = model.Name,
            ReportName = model.ReportName,
            Frequency = model.Frequency,
            DayOfWeek = model.DayOfWeek,
            DayOfMonth = model.DayOfMonth,
            Hour = model.Hour,
            Recipients = model.Recipients,
            AttachmentFormat = model.AttachmentFormat,
            IsEnabled = model.IsEnabled,
        }, subscription);

        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Key, error.Message);
        }

        return ModelState.IsValid;
    }

    private async Task<ReportSubscriptionEditViewModel> PopulateAsync(ReportSubscriptionEditViewModel model, ReportSubscription subscription)
    {
        model.Subscription = subscription;
        model.CanEmail = _service.CanSend;

        var reports = new List<SelectListItem>();

        foreach (var descriptor in _reports.GetReports())
        {
            if (await _authorizationService.AuthorizeAsync(User, descriptor.Permission))
            {
                var name = _localizerFactory.Localize(descriptor.DisplayName).Value;

                reports.Add(new SelectListItem(string.IsNullOrEmpty(descriptor.Category) ? name : $"{descriptor.Category} › {name}", descriptor.Name, descriptor.Name == model.ReportName));
            }
        }

        model.Reports = [.. reports.OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase)];
        model.Formats = [.. _exports.GetFormats().Select(format => new SelectListItem(_localizerFactory.Localize(format.DisplayName).Value, format.Name, format.Name == model.AttachmentFormat))];

        return model;
    }
}
