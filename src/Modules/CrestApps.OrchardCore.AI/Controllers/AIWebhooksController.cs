using CrestApps.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.AI.Controllers;

/// <summary>
/// Webhook subscriptions for AI and contact center events, and the log of what was sent to each.
/// </summary>
[Admin]
[Feature(AIConstants.Feature.Webhooks)]
public sealed class AIWebhooksController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIWebhookSubscriptionStore _store;
    private readonly IAIWebhookDeliveryStore _deliveryStore;
    private readonly AIWebhookPublisher _publisher;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIWebhookSubscription> _displayManager;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIWebhooksController"/> class.
    /// </summary>
    /// <param name="store">The subscription store.</param>
    /// <param name="deliveryStore">The delivery store.</param>
    /// <param name="publisher">Queues test events and redeliveries.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The subscription display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIWebhooksController(
        IAIWebhookSubscriptionStore store,
        IAIWebhookDeliveryStore deliveryStore,
        AIWebhookPublisher publisher,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIWebhookSubscription> displayManager,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<AIWebhooksController> htmlLocalizer,
        IStringLocalizer<AIWebhooksController> stringLocalizer)
    {
        _store = store;
        _deliveryStore = deliveryStore;
        _publisher = publisher;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the subscriptions.
    /// </summary>
    /// <param name="options">The search.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("ai/webhooks", "AIWebhooksIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _store.PageAsync(pager.Page, pager.PageSize, new QueryContext { Name = options.Search });
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        return View(new ListCatalogEntryViewModel
        {
            Options = options,
            List = new AdminListContext<AIWebhookSubscription>
            {
                Source = result.Entries,
                Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Applies the search.
    /// </summary>
    /// <param name="model">The submitted list.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/webhooks", "AIWebhooksIndex")]
    public async Task<IActionResult> IndexFilterPost(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Shows the form for a new subscription.
    /// </summary>
    /// <returns>The form.</returns>
    [Admin("ai/webhooks/create", "AIWebhooksCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["New webhook"],
            Editor = await _displayManager.BuildEditorAsync(new AIWebhookSubscription(), _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    /// <summary>
    /// Saves a new subscription.
    /// </summary>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/webhooks/create", "AIWebhooksCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = new AIWebhookSubscription
        {
            ItemId = UniqueId.GenerateId(),
            CreatedUtc = _clock.UtcNow,
        };

        var editor = await _displayManager.UpdateEditorAsync(subscription, _updateModelAccessor.ModelUpdater, isNew: true);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = S["New webhook"], Editor = editor });
        }

        await _store.CreateAsync(subscription);
        await _notifier.SuccessAsync(H["The webhook was created."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Shows a subscription's form.
    /// </summary>
    /// <param name="id">The subscription.</param>
    /// <returns>The form.</returns>
    [Admin("ai/webhooks/edit/{id}", "AIWebhooksEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = await _store.FindByIdAsync(id);

        if (subscription is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = subscription.Name,
            Editor = await _displayManager.BuildEditorAsync(subscription, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    /// <summary>
    /// Saves a subscription.
    /// </summary>
    /// <param name="id">The subscription.</param>
    /// <returns>A redirect to the list, or the form when it is not valid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/webhooks/edit/{id}", "AIWebhooksEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = await _store.FindByIdAsync(id);

        if (subscription is null)
        {
            return NotFound();
        }

        var editor = await _displayManager.UpdateEditorAsync(subscription, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!ModelState.IsValid)
        {
            return View(new EditCatalogEntryViewModel { DisplayName = subscription.Name, Editor = editor });
        }

        await _store.UpdateAsync(subscription);
        await _notifier.SuccessAsync(H["The webhook was saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a subscription. Its deliveries are kept.
    /// </summary>
    /// <param name="id">The subscription.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("ai/webhooks/delete/{id}", "AIWebhooksDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = await _store.FindByIdAsync(id);

        if (subscription is not null)
        {
            await _store.DeleteAsync(subscription);
            await _notifier.SuccessAsync(H["The webhook was deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Queues a test event for a subscription.
    /// </summary>
    /// <param name="id">The subscription.</param>
    /// <returns>A redirect to its deliveries.</returns>
    [HttpPost]
    [Admin("ai/webhooks/test/{id}", "AIWebhooksTest")]
    public async Task<IActionResult> Test(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = await _store.FindByIdAsync(id);

        if (subscription is null)
        {
            return NotFound();
        }

        await _publisher.QueueAsync(subscription, AIWebhookEvents.Test, new { message = "A test event from the admin." });
        await _notifier.SuccessAsync(H["A test event was queued. It is sent within a minute."]);

        return RedirectToAction(nameof(Deliveries), new { id });
    }

    /// <summary>
    /// Lists what was sent to a subscription, newest first.
    /// </summary>
    /// <param name="id">The subscription.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The delivery log.</returns>
    [Admin("ai/webhooks/deliveries/{id}", "AIWebhooksDeliveries")]
    public async Task<IActionResult> Deliveries(
        string id,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var subscription = await _store.FindByIdAsync(id);

        if (subscription is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (deliveries, count) = await _deliveryStore.PageAsync(id, pager.Page, pager.PageSize);

        return View(new AIWebhookDeliveriesViewModel
        {
            Subscription = subscription,
            List = new AdminListContext<AIWebhookDelivery>
            {
                Source = deliveries,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteData()),
            },
        });
    }

    /// <summary>
    /// Sends a delivery again, as a new delivery of the same body.
    /// </summary>
    /// <param name="deliveryId">The delivery.</param>
    /// <returns>A redirect to the subscription's deliveries.</returns>
    [HttpPost]
    [Admin("ai/webhooks/redeliver/{deliveryId}", "AIWebhooksRedeliver")]
    public async Task<IActionResult> Redeliver(string deliveryId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIWebhooks))
        {
            return Forbid();
        }

        var delivery = await _deliveryStore.FindByIdAsync(deliveryId);

        if (delivery is null)
        {
            return NotFound();
        }

        delivery.Status = AIWebhookDeliveryStatus.Pending;
        delivery.NextAttemptUtc = _clock.UtcNow;
        delivery.CompletedUtc = null;
        delivery.Attempts = 0;
        await _deliveryStore.UpdateAsync(delivery);
        await _notifier.SuccessAsync(H["The delivery is queued again. It is sent within a minute."]);

        return RedirectToAction(nameof(Deliveries), new { id = delivery.SubscriptionId });
    }
}
