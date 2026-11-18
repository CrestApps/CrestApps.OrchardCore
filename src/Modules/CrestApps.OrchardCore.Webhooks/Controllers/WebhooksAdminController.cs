using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Services;
using CrestApps.OrchardCore.Webhooks.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Webhooks.Controllers;

/// <summary>
/// Configuration › Webhooks: the URLs events are posted to, their secrets, and the log of what was sent.
/// </summary>
[Admin("webhooks/{action}/{itemId?}", "Webhooks{action}")]
public sealed class WebhooksAdminController : Controller
{
    private const string _optionsStatus = "Options.Status";

    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly WebhookDispatcher _dispatcher;
    private readonly IWebhookEventCatalog _events;
    private readonly WebhookSubscriptionCommands _commands;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public WebhooksAdminController(
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        WebhookDispatcher dispatcher,
        IWebhookEventCatalog events,
        WebhookSubscriptionCommands commands,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<WebhooksAdminController> htmlLocalizer,
        IStringLocalizer<WebhooksAdminController> stringLocalizer)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _events = events;
        _commands = commands;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("webhooks", "WebhooksIndex")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, subscriptions) = await _subscriptions.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var pagerShape = await shapeFactory.PagerAsync(pager, count, new RouteData());

        return View(new WebhooksIndexViewModel
        {
            List = new AdminListContext<WebhookSubscription>
            {
                Source = subscriptions,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        return View("Edit", Populate(new WebhookEditViewModel()));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(WebhookEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        Validate(model);

        if (!ModelState.IsValid)
        {
            return View("Edit", Populate(model));
        }

        var subscription = await _commands.CreateAsync(ToInput(model));

        // Its secret is on its page, to copy into the receiver.
        await _notifier.SuccessAsync(H["The webhook was added. Copy its signing secret into the system that receives it."]);

        return RedirectToAction(nameof(Edit), new { itemId = subscription.ItemId });
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        return View(Populate(new WebhookEditViewModel
        {
            Name = subscription.Name,
            Url = subscription.Url,
            IsEnabled = subscription.IsEnabled,
            AllEvents = subscription.Events.Contains(WebhookEvents.All),
            Events = [.. subscription.Events.Where(entry => entry != WebhookEvents.All)],
        }, subscription));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, WebhookEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        Validate(model);

        if (!ModelState.IsValid)
        {
            return View(Populate(model, subscription));
        }

        await _commands.UpdateAsync(subscription, ToInput(model));
        await _notifier.SuccessAsync(H["The webhook was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RotateSecret(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        await _commands.RotateSecretAsync(subscription);
        await _notifier.WarningAsync(H["The webhook has a new signing secret. Deliveries are signed with it from now on, so put it in the receiver."]);

        return RedirectToAction(nameof(Edit), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> Ping(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        var delivery = await _commands.PingAsync(subscription, HttpContext.RequestAborted);

        if (delivery.Status == WebhookDeliveryStatus.Delivered)
        {
            await _notifier.SuccessAsync(H["The receiver answered {0}.", delivery.LastStatusCode]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The receiver didn't take it: {0}", delivery.LastError ?? string.Empty]);
        }

        return RedirectToAction(nameof(Deliveries), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is { } subscription)
        {
            await _commands.DeleteAsync(subscription, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The webhook was deleted. Nothing more is sent to it."]);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Deliveries(
        string itemId,
        WebhookDeliveriesOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await FindSubscriptionAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        options ??= new WebhookDeliveriesOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, deliveries) = await _deliveries.PageAsync(subscription.ItemId, options.Status, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (options.Status is { } status)
        {
            routeData.Values.TryAdd(_optionsStatus, status);
        }

        options.Statuses =
        [
            new SelectListItem(S["Any status"], string.Empty, options.Status is null),
            new SelectListItem(S["Pending"], nameof(WebhookDeliveryStatus.Pending), options.Status == WebhookDeliveryStatus.Pending),
            new SelectListItem(S["Delivered"], nameof(WebhookDeliveryStatus.Delivered), options.Status == WebhookDeliveryStatus.Delivered),
            new SelectListItem(S["Failed"], nameof(WebhookDeliveryStatus.Failed), options.Status == WebhookDeliveryStatus.Failed),
        ];

        var pagerShape = await shapeFactory.PagerAsync(pager, count, routeData);

        return View(new WebhookDeliveriesViewModel
        {
            Subscription = subscription,
            Options = options,
            List = new AdminListContext<WebhookDelivery>
            {
                Source = deliveries,
                Pager = (IShape)pagerShape,
                ToolbarActions = await shapeFactory.CreateAsync("WebhookDeliveriesListFilters", Arguments.From(new { Options = options })),
            },
        });
    }

    [HttpPost]
    [ActionName(nameof(Deliveries))]
    public async Task<IActionResult> DeliveriesFilterPost(string itemId, WebhookDeliveriesOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary { ["itemId"] = itemId };

        if (options?.Status is { } status)
        {
            routeValues.TryAdd(_optionsStatus, status);
        }

        return RedirectToAction(nameof(Deliveries), routeValues);
    }

    [HttpPost]
    public async Task<IActionResult> Resend(string itemId, string deliveryId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WebhookPermissions.ManageWebhooks))
        {
            return Forbid();
        }

        if (await _deliveries.FindByIdAsync(deliveryId) is { } delivery && delivery.SubscriptionId == itemId)
        {
            await _dispatcher.ResendAsync(delivery, HttpContext.RequestAborted);

            if (delivery.Status == WebhookDeliveryStatus.Delivered)
            {
                await _notifier.SuccessAsync(H["Sent again; the receiver answered {0}.", delivery.LastStatusCode]);
            }
            else
            {
                await _notifier.WarningAsync(H["Sent again, but the receiver didn't take it: {0}. It's tried again later.", delivery.LastError ?? string.Empty]);
            }
        }

        return RedirectToAction(nameof(Deliveries), new { itemId });
    }

    private void Validate(WebhookEditViewModel model)
    {
        foreach (var error in _commands.Validate(ToInput(model)))
        {
            ModelState.AddModelError(error.Key, error.Message);
        }
    }

    /// <summary>
    /// Returns whether events may be posted to a URL: an absolute https address; plain http only to this machine while
    /// developing.
    /// </summary>
    internal static bool IsAllowedUrl(string url, bool isDevelopment)
        => WebhookSubscriptionCommands.IsAllowedUrl(url, isDevelopment);

    private static WebhookSubscriptionInput ToInput(WebhookEditViewModel model)
        => new()
        {
            Name = model.Name,
            Url = model.Url,
            IsEnabled = model.IsEnabled,
            AllEvents = model.AllEvents,
            Events = model.Events,
        };

    private WebhookEditViewModel Populate(WebhookEditViewModel model, WebhookSubscription subscription = null)
    {
        model.ItemId = subscription?.ItemId;
        model.Secret = subscription is null ? null : _dispatcher.GetSecret(subscription);
        model.AvailableEvents = [.. _events.GetEvents().GroupBy(entry => entry.Group)];

        return model;
    }

    // A missing or empty id (a hand-edited URL) is a page that does not exist, not a server error.
    private async Task<WebhookSubscription> FindSubscriptionAsync(string itemId)
        => string.IsNullOrWhiteSpace(itemId) ? null : await _subscriptions.FindByIdAsync(itemId);
}
