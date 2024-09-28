using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Extensions;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

public sealed class SubscriptionsController : Controller
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubscriptionFlow> _subscriptionFlowDisplayManager;
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly ILogger<SubscriptionsController> _logger;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly ISession _session;

    internal readonly IHtmlLocalizer H;

    public SubscriptionsController(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubscriptionFlow> subscriptionFlowDisplayManager,
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        ILogger<SubscriptionsController> logger,
        IClientIPAddressAccessor clientIPAddressAccessor,
        ISubscriptionSessionStore subscriptionSessionStore,
        INotifier notifier,
        IClock clock,
        ISession session,
        IHtmlLocalizer<SubscriptionsController> htmlLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _subscriptionFlowDisplayManager = subscriptionFlowDisplayManager;
        _subscriptionHandlers = subscriptionHandlers;
        _logger = logger;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _subscriptionSessionStore = subscriptionSessionStore;
        _notifier = notifier;
        _clock = clock;
        _session = session;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Index(
        string contentType,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var contentTypes = new List<string>();

        if (!string.IsNullOrEmpty(contentType))
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition == null || !definition.StereotypeEquals(SubscriptionConstants.Stereotype))
            {
                return NotFound();
            }

            contentTypes.Add(definition.Name);
        }

        if (contentTypes.Count == 0)
        {
            contentTypes.AddRange((await _contentDefinitionManager.GetSubscriptionsTypeDefinitionsAsync()).Select(x => x.Name));
        }

        if (contentTypes.Count == 0)
        {
            return NotFound();
        }

        var query = _session.Query<ContentItem, SubscriptionsContentItemIndex>(item => item.Published && item.ContentType.IsIn(contentTypes))
            .OrderBy(index => index.Order)
            .ThenByDescending(index => index.CreatedUtc);

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var total = await query.CountAsync();

        var pagerShape = await shapeFactory.PagerAsync(pager, total);

        var startIndex = (pager.Page - 1) * pager.PageSize;

        var contentItems = await query.Skip(startIndex).Take(pager.PageSize).ListAsync();

        var model = new ListSubscriptionsViewModel()
        {
            Pager = pagerShape,
            Subscriptions = []
        };

        foreach (var contentItem in contentItems)
        {
            var shape = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, _updateModelAccessor.ModelUpdater, "Summary");

            model.Subscriptions.Add(shape);
        }

        return View(model);
    }

    /// <summary>
    /// Generate a new signup session for the given subscription id.
    /// </summary>
    /// <param name="contentItemId">The content item that represent the subscription.</param>
    public async Task<IActionResult> Signup(string contentItemId)
    {
        var subscriptionContentItem = await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.Published && index.ContentItemId == contentItemId)
            .FirstOrDefaultAsync();

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        var subscriptionSession = await _subscriptionSessionStore.NewAsync(subscriptionContentItem);

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializingAsync(context), new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem), _logger);
        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadingAsync(context), new SubscriptionFlowLoadingContext(flow), _logger);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializedAsync(context), new SubscriptionFlowInitializedContext(flow), _logger);

        subscriptionSession.CurrentStep ??= flow.GetFirstStep()?.Key;

        var model = await _subscriptionFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadedAsync(context), new SubscriptionFlowLoadedContext(flow), _logger);

        return View(new SubscriptionViewModel
        {
            ContentItemId = contentItemId,
            SessionId = null,
            Step = subscriptionSession.CurrentStep,
            Content = model,
        });
    }

    /// <summary>
    /// Save Session.
    /// </summary>
    /// <param name="sessionId">The current sessionId.</param>
    /// <param name="step">The current step the user came from.</param>
    /// <returns></returns>
    [HttpPost]
    [ActionName(nameof(Signup))]
    public async Task<IActionResult> SignupPOST(SubscriptionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ContentItemId))
        {
            return NotFound();
        }

        var subscriptionContentItem = await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.Published && index.ContentItemId == model.ContentItemId)
            .FirstOrDefaultAsync();

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        SubscriptionSession subscriptionSession = null;

        if (!string.IsNullOrWhiteSpace(model.SessionId))
        {
            subscriptionSession = await _subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

            if (subscriptionSession != null &&
                !string.IsNullOrEmpty(model.Step) &&
                !string.Equals(model.Step, subscriptionSession.CurrentStep, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var savedStep in subscriptionSession.SavedSteps)
                {
                    if (!string.Equals(model.Step, savedStep.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // The requested step exists in the saved steps.
                    // Set the current step to the requested step, ensuring that the user is directed
                    // to the next screen based on their navigation path, rather than where the session suggests they should be.
                    // We use 'savedStep.Key' instead of 'step' to ensure the correct case-sensitive value is passed.
                    subscriptionSession.CurrentStep = savedStep.Key;
                }
            }
        }

        subscriptionSession ??= await _subscriptionSessionStore.NewAsync(subscriptionContentItem);

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializingAsync(context), new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem), _logger);

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadingAsync(context), new SubscriptionFlowLoadingContext(flow), _logger);

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializedAsync(context), new SubscriptionFlowInitializedContext(flow), _logger);

        var shape = await _subscriptionFlowDisplayManager.UpdateEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        if (_updateModelAccessor.ModelUpdater.ModelState.IsValid)
        {
            var cookieManager = new SubscriptionCookieManager(HttpContext);
            cookieManager.Append(model.ContentItemId, subscriptionSession.SessionId);
            var now = _clock.UtcNow;

            subscriptionSession.ModifiedUtc = now;

            // If the upcoming step is null "meaning we are not navigating back", get the next step if one exists.
            var upcomingStep = flow.GetNextStep();

            if (upcomingStep != null)
            {
                flow.SetCurrentStep(upcomingStep.Key);

                await _subscriptionSessionStore.SaveAsync(subscriptionSession);

                return RedirectToAction(nameof(Display), new
                {
                    sessionId = subscriptionSession.SessionId,
                    step = upcomingStep.Key,
                });
            }
            else
            {
                // Ensure all steps have data.
                foreach (var sortedStep in flow.GetSortedSteps())
                {
                    if (sortedStep.CollectData && !subscriptionSession.SavedSteps.ContainsKey(sortedStep.Key))
                    {
                        // This step is not completed. Redirect the user to this step.
                        flow.SetCurrentStep(sortedStep.Key);

                        await _subscriptionSessionStore.SaveAsync(subscriptionSession);

                        return RedirectToAction(nameof(Display), new
                        {
                            sessionId = subscriptionSession.SessionId,
                            step = sortedStep.Key,
                        });
                    }
                }

                try
                {
                    // The 'CompletingAsync' could throw exception, do not use 'InvokeAsync'
                    // to catch exceptions here and rollback.
                    var completingContext = new SubscriptionFlowCompletingContext(flow);

                    foreach (var handler in _subscriptionHandlers)
                    {
                        await handler.CompletingAsync(completingContext);
                    }

                    subscriptionSession.Status = SubscriptionSessionStatus.Completed;
                    subscriptionSession.CompletedUtc = now;

                    await _subscriptionSessionStore.SaveAsync(subscriptionSession);
                    await _session.SaveChangesAsync();

                    await _subscriptionHandlers.InvokeAsync(
                        (handler, context) => handler.CompletedAsync(context), new SubscriptionFlowCompletedContext(flow), _logger);

                    cookieManager.Remove(model.ContentItemId);

                    return RedirectToAction(nameof(Confirmation), new
                    {
                        sessionId = subscriptionSession.SessionId,
                    });
                }
                catch (Exception ex)
                {
                    await _session.CancelAsync();
                    await _subscriptionHandlers.InvokeAsync(
                        (handler, context) => handler.FailedAsync(context), new SubscriptionFlowFailedContext(flow), _logger);
                    _logger.LogError(ex, "Unable to completed a subscription");

                    await _notifier.ErrorAsync(H["Unable to process the subscription at this time. If the issue persists, please contact support."]);
                }
            }

            model.Step = flow.GetCurrentStep()?.Key;
            model.Content = shape;

            return View(model);
        }

        return View(new SubscriptionViewModel
        {
            ContentItemId = model.ContentItemId,
            // At this point sessionId does not belong to a saved session. So we set it to null.
            SessionId = null,
            Step = flow.GetCurrentStep()?.Key,
            Content = shape,
        });
    }

    public async Task<IActionResult> Display(string sessionId, string step)
    {
        var subscriptionSession = await _subscriptionSessionStore.GetAsync(sessionId, SubscriptionSessionStatus.Pending);

        if (subscriptionSession == null)
        {
            return NotFound();
        }

        var subscriptionContentItem = await GetSubscriptionVersion(subscriptionSession.ContentItemVersionId);

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        // If the user requests a specific step,
        // make sure it is a completed step before rendering it.
        if (!string.IsNullOrEmpty(step) && subscriptionSession.SavedSteps.ContainsKey(step))
        {
            subscriptionSession.CurrentStep = step;
        }

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializingAsync(context), new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem), _logger);
        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadingAsync(context), new SubscriptionFlowLoadingContext(flow), _logger);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializedAsync(context), new SubscriptionFlowInitializedContext(flow), _logger);

        var shape = await _subscriptionFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, false);

        return View(nameof(Signup), new SubscriptionViewModel
        {
            ContentItemId = subscriptionContentItem.ContentItemId,
            SessionId = subscriptionSession.SessionId,
            Step = subscriptionSession.CurrentStep,
            Content = shape,
        });
    }

    public async Task<IActionResult> Confirmation(string sessionId)
    {
        var subscriptionSession = await _subscriptionSessionStore.GetAsync(sessionId, SubscriptionSessionStatus.Completed);

        if (subscriptionSession == null)
        {
            return NotFound();
        }

        var subscriptionContentItem = await GetSubscriptionVersion(subscriptionSession.ContentItemVersionId);

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializingAsync(context), new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem), _logger);
        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadingAsync(context), new SubscriptionFlowLoadingContext(flow), _logger);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializedAsync(context), new SubscriptionFlowInitializedContext(flow), _logger);

        var confirmation = await _subscriptionFlowDisplayManager.BuildDisplayAsync(flow, _updateModelAccessor.ModelUpdater, "Confirmation");

        return View(confirmation);
    }

    private async Task<ContentItem> GetSubscriptionVersion(string versionContentItemId)
        => await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.ContentItemVersionId == versionContentItemId)
        .FirstOrDefaultAsync();

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
