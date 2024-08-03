using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Extensions;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
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
    private readonly IClock _clock;
    private readonly ISession _session;

    public SubscriptionsController(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubscriptionFlow> subscriptionFlowDisplayManager,
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        ILogger<SubscriptionsController> logger,
        IClientIPAddressAccessor clientIPAddressAccessor,
        IClock clock,
        ISession session)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _subscriptionFlowDisplayManager = subscriptionFlowDisplayManager;
        _subscriptionHandlers = subscriptionHandlers;
        _logger = logger;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _clock = clock;
        _session = session;
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

            if (definition == null || !definition.StereotypeEquals(SubscriptionsConstants.Stereotype))
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

        //var items = await _session.Query<SubscriptionSession, SubscriptionSessionIndex>().ListAsync();

        //foreach (var item in items)
        //{
        //    _session.Delete(item);
        //}
        //await _session.SaveChangesAsync();
        SubscriptionSession subscriptionSession = null;

        if (User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();
            var status = nameof(SubscriptionSessionStatus.Pending);
            var modifiedUtc = _clock.UtcNow.AddDays(-1);

            subscriptionSession = await _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.ContentItemVersionId == subscriptionContentItem.ContentItemVersionId && x.OwnerId == ownerId && x.Status == status && x.ModifiedUtc > modifiedUtc)
                .OrderByDescending(x => x.ModifiedUtc)
                .FirstOrDefaultAsync();
        }

        if (subscriptionSession == null)
        {
            var now = _clock.UtcNow;

            subscriptionSession = new SubscriptionSession()
            {
                SessionId = IdGenerator.GenerateId(),
                ContentItemId = subscriptionContentItem.ContentItemId,
                ContentItemVersionId = subscriptionContentItem.ContentItemVersionId,
                CreatedUtc = now,
                ModifiedUtc = now,
                Status = SubscriptionSessionStatus.Pending,
            };

            if (User.Identity.IsAuthenticated)
            {
                subscriptionSession.OwnerId = CurrentUserId();
            }
            else
            {
                subscriptionSession.IPAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();
                subscriptionSession.AgentInfo = Request.Headers.UserAgent;
            }

            var initializationContext = new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem);
            await _subscriptionHandlers.InvokeAsync((handler, context) => handler.InitializingAsync(context), initializationContext, _logger);
        }

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        subscriptionSession.CurrentStep ??= flow.GetFirstStep()?.Key;

        var model = await _subscriptionFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        var loadedContext = new SubscriptionFlowLoadedContext(flow);
        await _subscriptionHandlers.InvokeAsync((handler, context) => handler.LoadedAsync(context), loadedContext, _logger);

        await _session.SaveAsync(subscriptionSession);

        return View(new SubscriptionViewModel
        {
            SessionId = subscriptionSession.SessionId,
            Step = subscriptionSession.CurrentStep,
            Content = model,
        });
    }

    [HttpPost]
    [ActionName(nameof(Signup))]
    public async Task<IActionResult> SignupPOST(string sessionId, string step, string direction)
    {
        var subscriptionSession = await GetSessionAsync(sessionId, nameof(SubscriptionSessionStatus.Pending));

        if (subscriptionSession == null)
        {
            return NotFound();
        }

        var subscriptionContentItem = await GetSubscriptionVersion(subscriptionSession.ContentItemVersionId);

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        var isGoingBack = string.Equals(direction, "Previous", StringComparison.OrdinalIgnoreCase);

        // If the user requests a specific step,
        // make sure it is a completed step before rendering it.
        if (!isGoingBack && !string.IsNullOrEmpty(step) && subscriptionSession.SavedSteps.ContainsKey(step))
        {
            subscriptionSession.CurrentStep = step;
        }

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);

        if (isGoingBack)
        {
            var previousStep = flow.GetPreviousStep();

            if (previousStep != null)
            {
                flow.SetCurrentStep(previousStep.Key);
            }
        }

        var model = await _subscriptionFlowDisplayManager.UpdateEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        if (_updateModelAccessor.ModelUpdater.ModelState.IsValid)
        {
            var now = _clock.UtcNow;

            subscriptionSession.ModifiedUtc = now;

            var nextStep = flow.GetNextStep();

            if (nextStep != null)
            {
                flow.SetCurrentStep(nextStep.Key);

                await _session.SaveAsync(subscriptionSession);

                return RedirectToAction(nameof(ViewSession), new
                {
                    sessionId,
                    step = nextStep.Key,
                });
            }
            else
            {
                subscriptionSession.Status = SubscriptionSessionStatus.Completed;
                subscriptionSession.CompletedUtc = now;

                var completedContext = new SubscriptionFlowCompletedContext(flow);

                await _subscriptionHandlers.InvokeAsync((handler, context) => handler.CompletedAsync(context), completedContext, _logger);

                try
                {
                    // TODO: process payment here.
                    // Show success notification.
                    // Redirect the user to thank you page!
                    await _session.SaveAsync(subscriptionSession);
                    await _session.SaveChangesAsync();

                    return RedirectToAction(nameof(Confirmation), new
                    {
                        sessionId,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to completed a subscription");
                    // TODO: rollback payment here.
                    // Show error notification.
                }
            }
        }

        return View(new SubscriptionViewModel
        {
            SessionId = subscriptionSession.SessionId,
            Step = flow.GetCurrentStep()?.Key,
            Content = model,
        });
    }

    public async Task<IActionResult> ViewSession(string sessionId, string step)
    {
        var subscriptionSession = await GetSessionAsync(sessionId, nameof(SubscriptionSessionStatus.Pending));

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

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);

        var model = await _subscriptionFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, false);

        return View(nameof(Signup), new SubscriptionViewModel
        {
            SessionId = subscriptionSession.SessionId,
            Step = subscriptionSession.CurrentStep,
            Content = model,
        });
    }

    public async Task<IActionResult> Confirmation(string sessionId)
    {
        var subscriptionSession = await GetSessionAsync(sessionId, nameof(SubscriptionSessionStatus.Completed));

        if (subscriptionSession == null)
        {
            return NotFound();
        }

        var subscriptionContentItem = await GetSubscriptionVersion(subscriptionSession.ContentItemVersionId);

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);

        var confirmation = await _subscriptionFlowDisplayManager.BuildDisplayAsync(flow, _updateModelAccessor.ModelUpdater, "Confirmation");

        return View(confirmation);
    }

    private async Task<ContentItem> GetSubscriptionVersion(string versionContentItemId)
        => await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.ContentItemVersionId == versionContentItemId)
        .FirstOrDefaultAsync();

    private async Task<SubscriptionSession> GetSessionAsync(string sessionId, string status)
    {
        SubscriptionSession subscriptionSession = null;

        var query = _session.Query<SubscriptionSession, SubscriptionSessionIndex>(x => x.SessionId == sessionId && x.Status == status);

        if (User.Identity.IsAuthenticated)
        {
            var ownerId = CurrentUserId();

            subscriptionSession = await query.Where(x => x.OwnerId == ownerId).FirstOrDefaultAsync();
        }
        else
        {
            subscriptionSession = await query.FirstOrDefaultAsync();

            // Don't trust the user, check for additional info.
            var ipAddress = (await _clientIPAddressAccessor.GetIPAddressAsync()).ToString();

            if (string.IsNullOrWhiteSpace(subscriptionSession?.IPAddress) ||
                subscriptionSession.IPAddress != ipAddress ||
                string.IsNullOrWhiteSpace(subscriptionSession?.AgentInfo) ||
                subscriptionSession.AgentInfo != Request.Headers.UserAgent)
            {
                // IMPORTANT: the saved session possibly belongs to someone else.
                // Do not use it.
                subscriptionSession = null;
            }
        }

        return subscriptionSession;
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
