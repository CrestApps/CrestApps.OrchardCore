using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Webhooks.Models;
using CrestApps.OrchardCore.Commerce.Webhooks.Services;
using CrestApps.OrchardCore.Commerce.Webhooks.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Commerce.Webhooks.Controllers;

/// <summary>
/// Commerce › Webhooks: the URLs events are posted to, their secrets, and the log of what was sent.
/// </summary>
[Admin("commerce/webhooks/{action}/{itemId?}", "CommerceWebhooks{action}")]
public sealed class WebhooksAdminController : Controller
{
    private const string _optionsStatus = "Options.Status";

    private readonly IWebhookSubscriptionStore _subscriptions;
    private readonly IWebhookDeliveryStore _deliveries;
    private readonly WebhookDispatcher _dispatcher;
    private readonly CommerceWebhookOptions _options;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public WebhooksAdminController(
        IWebhookSubscriptionStore subscriptions,
        IWebhookDeliveryStore deliveries,
        WebhookDispatcher dispatcher,
        IOptions<CommerceWebhookOptions> options,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHostEnvironment hostEnvironment,
        IClock clock,
        IHtmlLocalizer<WebhooksAdminController> htmlLocalizer,
        IStringLocalizer<WebhooksAdminController> stringLocalizer)
    {
        _subscriptions = subscriptions;
        _deliveries = deliveries;
        _dispatcher = dispatcher;
        _options = options.Value;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _hostEnvironment = hostEnvironment;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("commerce/webhooks", "CommerceWebhooksIndex")]
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

        var now = _clock.UtcNow;
        var subscription = new WebhookSubscription
        {
            ItemId = IdGenerator.GenerateId(),
            ProtectedSecret = _dispatcher.NewProtectedSecret(),
            CreatedUtc = now,
        };

        Apply(model, subscription);
        await _subscriptions.CreateAsync(subscription);

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

        if (await _subscriptions.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        return View(Populate(new WebhookEditViewModel
        {
            Name = subscription.Name,
            Url = subscription.Url,
            IsEnabled = subscription.IsEnabled,
            AllEvents = subscription.Events.Contains(WebhookConstants.AllEvents),
            Events = [.. subscription.Events.Where(entry => entry != WebhookConstants.AllEvents)],
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

        if (await _subscriptions.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        Validate(model);

        if (!ModelState.IsValid)
        {
            return View(Populate(model, subscription));
        }

        Apply(model, subscription);
        await _subscriptions.UpdateAsync(subscription);
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

        if (await _subscriptions.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        subscription.ProtectedSecret = _dispatcher.NewProtectedSecret();
        subscription.ModifiedUtc = _clock.UtcNow;
        await _subscriptions.UpdateAsync(subscription);
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

        if (await _subscriptions.FindByIdAsync(itemId) is not { } subscription)
        {
            return NotFound();
        }

        var now = _clock.UtcNow;
        var eventId = "evt_" + IdGenerator.GenerateId();
        var delivery = new WebhookDelivery
        {
            ItemId = IdGenerator.GenerateId(),
            SubscriptionId = subscription.ItemId,
            EventId = eventId,
            EventName = CommerceWebhookEvents.Ping,
            Payload = JsonSerializer.Serialize(new { id = eventId, @event = CommerceWebhookEvents.Ping, createdUtc = now, data = new { webhookId = subscription.ItemId, name = subscription.Name } }, JsonSerializerOptions.Web),
            Status = WebhookDeliveryStatus.Pending,
            NextAttemptUtc = now,
            CreatedUtc = now,
        };

        await _deliveries.CreateAsync(delivery, HttpContext.RequestAborted);
        await _dispatcher.SendAsync(delivery, HttpContext.RequestAborted);

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

        if (await _subscriptions.FindByIdAsync(itemId) is { } subscription)
        {
            await _subscriptions.DeleteAsync(subscription);

            // Its log goes with it; what's left over is cleared with the log's old entries.
            await _deliveries.DeleteBySubscriptionAsync(subscription.ItemId, take: 1000, HttpContext.RequestAborted);
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

        if (await _subscriptions.FindByIdAsync(itemId) is not { } subscription)
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
    [FormValueRequired("submit.Filter")]
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
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Name the webhook, such as the system it feeds."]);
        }

        if (!IsAllowedUrl(model.Url, _hostEnvironment.IsDevelopment()))
        {
            ModelState.AddModelError(nameof(model.Url), S["Enter the full https:// address events are posted to."]);
        }

        var known = _options.Events.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

        if (!model.AllEvents && !(model.Events ?? []).Any(known.Contains))
        {
            ModelState.AddModelError(nameof(model.Events), S["Choose the events to send, or all of them."]);
        }
    }

    /// <summary>
    /// Returns whether events may be posted to a URL: an absolute https address; plain http only to this machine while
    /// developing.
    /// </summary>
    internal static bool IsAllowedUrl(string url, bool isDevelopment)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // An address of the site's own network (a private, link-local or loopback IP) isn't another system's.
        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && WebhookNetwork.IsInternal(literal))
        {
            return isDevelopment && uri.Scheme == Uri.UriSchemeHttp && IPAddress.IsLoopback(literal);
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return !uri.IsLoopback;
        }

        return isDevelopment &&
            uri.Scheme == Uri.UriSchemeHttp &&
            (uri.IsLoopback || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)));
    }

    private void Apply(WebhookEditViewModel model, WebhookSubscription subscription)
    {
        var known = _options.Events.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

        subscription.Name = model.Name.Trim();
        subscription.Url = model.Url.Trim();
        subscription.IsEnabled = model.IsEnabled;
        subscription.Events = model.AllEvents
            ? [WebhookConstants.AllEvents]
            : [.. (model.Events ?? []).Where(known.Contains).Distinct(StringComparer.Ordinal)];
        subscription.ModifiedUtc = _clock.UtcNow;
    }

    private WebhookEditViewModel Populate(WebhookEditViewModel model, WebhookSubscription subscription = null)
    {
        model.ItemId = subscription?.ItemId;
        model.Secret = subscription is null ? null : _dispatcher.GetSecret(subscription);
        model.AvailableEvents = [.. _options.Events.GroupBy(entry => entry.Group)];

        return model;
    }
}
