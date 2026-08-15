using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

public sealed class SubscriptionsController : Controller
{
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<SubscriptionFlow> _subscriptionFlowDisplayManager;
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly ILogger<SubscriptionsController> _logger;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly ISession _session;
    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly IDistributedLock _distributedLock;

    internal readonly IHtmlLocalizer H;

    public SubscriptionsController(
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<SubscriptionFlow> subscriptionFlowDisplayManager,
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        ILogger<SubscriptionsController> logger,
        ISubscriptionSessionStore subscriptionSessionStore,
        INotifier notifier,
        IClock clock,
        ISession session,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IDistributedLock distributedLock,
        IHtmlLocalizer<SubscriptionsController> htmlLocalizer)
    {
        _updateModelAccessor = updateModelAccessor;
        _subscriptionFlowDisplayManager = subscriptionFlowDisplayManager;
        _subscriptionHandlers = subscriptionHandlers;
        _logger = logger;
        _subscriptionSessionStore = subscriptionSessionStore;
        _notifier = notifier;
        _clock = clock;
        _session = session;
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _distributedLock = distributedLock;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Generate a new signup session for the given subscription id.
    /// </summary>
    /// <param name="contentItemId">The content item that represent the subscription.</param>
    [HttpGet("Subscription/Signup/{contentItemId}", Name = "SubscriptionSignup")]
    public async Task<IActionResult> Signup(string contentItemId)
    {
        var subscriptionContentItem = await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.Published && index.ContentItemId == contentItemId)
            .FirstOrDefaultAsync();

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        var cookieManager = new SubscriptionCookieManager(HttpContext);

        SubscriptionSession subscriptionSession = null;

        if (cookieManager.TryGetValue(contentItemId, out var existingSessionId) && !string.IsNullOrEmpty(existingSessionId))
        {
            subscriptionSession = await _subscriptionSessionStore.GetAsync(existingSessionId, SubscriptionSessionStatus.Pending);

            // Only resume a persisted session when it still targets the current published plan version.
            if (subscriptionSession != null &&
                !string.Equals(subscriptionSession.ContentItemVersionId, subscriptionContentItem.ContentItemVersionId, StringComparison.Ordinal))
            {
                subscriptionSession = null;
            }
        }

        // Track whether this request created the session. A resumed session is already durable, so it must
        // not be re-saved here: a concurrent checkout return could flip it to Completed between our load and
        // save, and a last-write-wins save would revert it to Pending, dropping payment metadata and risking
        // a double charge.
        var isNewSession = subscriptionSession == null;

        subscriptionSession ??= await _subscriptionSessionStore.NewAsync(subscriptionContentItem);

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

        // Persist only a newly created pending session so the payment step (Stripe hosted checkout, card, or
        // pay later) can reference a durable, distributed-safe session when it calls the payment endpoints.
        // The cookie lets a returning visitor resume that same pending session instead of orphaning a new one
        // on every visit.
        if (isNewSession)
        {
            subscriptionSession.ModifiedUtc = _clock.UtcNow;
            await _subscriptionSessionStore.SaveAsync(subscriptionSession);
        }

        cookieManager.Append(contentItemId, subscriptionSession.SessionId);

        return View(new ServicePlanSubscriptionViewModel
        {
            ContentItemId = contentItemId,
            SessionId = subscriptionSession.SessionId,
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
    [HttpPost("Subscription/Signup/{contentItemId?}")]
    [ActionName(nameof(Signup))]
    [RateLimitGroup(SubscriptionConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> SignupPOST(ServicePlanSubscriptionViewModel model)
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

                if (await TryCompleteFlowUnderLockAsync(flow, subscriptionSession, now))
                {
                    cookieManager.Remove(model.ContentItemId);

                    return RedirectToAction(nameof(Confirmation), new
                    {
                        sessionId = subscriptionSession.SessionId,
                    });
                }
            }

            model.Step = flow.GetCurrentStep()?.Key;
            model.Content = shape;

            return View(model);
        }

        return View(new ServicePlanSubscriptionViewModel
        {
            ContentItemId = model.ContentItemId,
            // At this point sessionId does not belong to a saved session. So we set it to null.
            SessionId = null,
            Step = flow.GetCurrentStep()?.Key,
            Content = shape,
        });
    }

    [Route("Subscription/Step/{sessionId}", Name = "SubscriptionSignupStep")]
    [RateLimitGroup(SubscriptionConstants.RateLimitGroups.Checkout)]
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

        return View(nameof(Signup), new ServicePlanSubscriptionViewModel
        {
            ContentItemId = subscriptionContentItem.ContentItemId,
            SessionId = subscriptionSession.SessionId,
            Step = subscriptionSession.CurrentStep,
            Content = shape,
        });
    }

    [Route("Subscription/Confirmation/{sessionId}", Name = "SubscriptionConfirmation")]
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

    /// <summary>
    /// Completes a subscription flow: runs the completing handlers, marks the session completed and
    /// runs the completed handlers. On failure it rolls back and notifies the user. This is shared by
    /// the standard form post and by the hosted-checkout return so both finalize identically.
    /// </summary>
    private async Task<bool> TryCompleteFlowUnderLockAsync(SubscriptionFlow flow, SubscriptionSession session, DateTime now)
    {
        // Serialize finalization per local session so two concurrent submissions (double click, duplicate
        // POST, or a Pay Later submit racing a Stripe return) cannot both run the completion handlers and
        // double-provision (users, content, tenants). The same lock key is used by the Stripe checkout
        // return so those two finalization paths are mutually exclusive as well.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            $"SUBSCRIPTION_CHECKOUT_RETURN_{session.SessionId}",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(5));

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            // If another request already finalized this session, treat it as an idempotent success so the
            // caller redirects to the confirmation instead of re-running the provisioning handlers.
            var current = await _subscriptionSessionStore.GetAsync(session.SessionId);

            if (current != null && current.Status == SubscriptionSessionStatus.Completed)
            {
                return true;
            }

            return await TryCompleteFlowAsync(flow, session, now);
        }
    }

    private async Task<bool> TryCompleteFlowAsync(SubscriptionFlow flow, SubscriptionSession session, DateTime now)
    {
        try
        {
            // The 'CompletingAsync' could throw exception, do not use 'InvokeAsync'
            // to catch exceptions here and rollback.
            var completingContext = new SubscriptionFlowCompletingContext(flow);

            foreach (var handler in _subscriptionHandlers)
            {
                await handler.CompletingAsync(completingContext);
            }

            session.Status = SubscriptionSessionStatus.Completed;
            session.CompletedUtc = now;

            await _subscriptionSessionStore.SaveAsync(session);
            await _session.SaveChangesAsync();

            await _subscriptionHandlers.InvokeAsync(
                (handler, context) => handler.CompletedAsync(context), new SubscriptionFlowCompletedContext(flow), _logger);

            return true;
        }
        catch (Exception ex)
        {
            await _session.CancelAsync();
            await _subscriptionHandlers.InvokeAsync(
                (handler, context) => handler.FailedAsync(context), new SubscriptionFlowFailedContext(flow), _logger);
            _logger.LogError(ex, "Unable to completed a subscription");

            await _notifier.ErrorAsync(H["Unable to process the subscription at this time. If the issue persists, please contact support."]);

            return false;
        }
    }

    /// <summary>
    /// The URL Stripe redirects to after a customer completes (or the browser returns from) a hosted
    /// Stripe Checkout. It records the Stripe subscription against the local session and finalizes the flow.
    /// </summary>
    [Route("Subscription/CheckoutReturn/{sessionId}", Name = "SubscriptionCheckoutReturn")]
    public async Task<IActionResult> CheckoutReturn(string sessionId, string checkoutSessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return NotFound();
        }

        // Serialize finalization per local session so two concurrent returns (duplicate redirects,
        // double clicks, or a ret/replay) cannot both pass validation and run the completion handlers,
        // which would otherwise double-provision (users, content, tenants) or race the session status.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            $"SUBSCRIPTION_CHECKOUT_RETURN_{sessionId}",
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(5));

        if (!locked)
        {
            await _notifier.ErrorAsync(H["Your payment is still being processed. Please wait a moment before trying again."]);

            return RedirectToAction(nameof(Display), new
            {
                sessionId,
                step = SubscriptionConstants.StepKey.Payment,
            });
        }

        await using (locker)
        {
            return await CheckoutReturnCoreAsync(sessionId, checkoutSessionId);
        }
    }

    private async Task<IActionResult> CheckoutReturnCoreAsync(string sessionId, string checkoutSessionId)
    {
        // Loaded as Pending inside the lock: once the first finalize flips the status to Completed, a
        // concurrent/duplicate return re-reads null here and stops, so finalization runs exactly once.
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

        var checkoutService = HttpContext.RequestServices.GetService<IStripeCheckoutService>();

        if (checkoutService == null || string.IsNullOrEmpty(checkoutSessionId))
        {
            return NotFound();
        }

        var details = await checkoutService.GetAsync(checkoutSessionId);

        subscriptionSession.TryGet<Invoice>(out var invoice);

        // Never trust a client-supplied checkout id: only finalize when Stripe confirms this exact session
        // is complete, paid, references THIS local session (via the client reference id we set at creation)
        // and was charged in the invoice currency. Binding to the client reference id prevents a valid
        // checkout for one session from being replayed to finalize a different local session.
        var validation = HostedCheckoutReturnValidator.Validate(details, subscriptionSession.SessionId, invoice?.Currency);

        if (validation != CheckoutReturnValidation.Valid || invoice == null)
        {
            if (validation == CheckoutReturnValidation.CurrencyMismatch)
            {
                _logger.LogWarning(
                    "Checkout return for session '{SessionId}' rejected: Stripe currency '{StripeCurrency}' does not match invoice currency '{InvoiceCurrency}'.",
                    subscriptionSession.SessionId, details.Currency, invoice?.Currency);
            }

            await _notifier.ErrorAsync(H["Your payment could not be confirmed. Please try again."]);

            return RedirectToAction(nameof(Display), new
            {
                sessionId = subscriptionSession.SessionId,
                step = SubscriptionConstants.StepKey.Payment,
            });
        }

        var groups = invoice.GetSubscriptionGroups();

        // A hosted checkout maps to a single Stripe subscription; this is enforced when the session is created.
        if (groups.Count != 1)
        {
            await _notifier.ErrorAsync(H["This product cannot be completed with hosted checkout."]);

            return RedirectToAction(nameof(Display), new
            {
                sessionId = subscriptionSession.SessionId,
                step = SubscriptionConstants.StepKey.Payment,
            });
        }

        var now = _clock.UtcNow;
        var group = groups.First();
        var expiresAt = BillingSchedule.GetNextBillingDate(now, group.Key.Type, group.Key.Duration);
        var gatewayMode = details.Livemode ? GatewayMode.Live : GatewayMode.Testing;

        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializingAsync(context), new SubscriptionFlowInitializingContext(subscriptionSession, subscriptionContentItem), _logger);
        var flow = new SubscriptionFlow(subscriptionSession, subscriptionContentItem);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.LoadingAsync(context), new SubscriptionFlowLoadingContext(flow), _logger);
        await _subscriptionHandlers.InvokeAsync(
            (handler, context) => handler.InitializedAsync(context), new SubscriptionFlowInitializedContext(flow), _logger);

        // Record the Stripe subscription so the confirmation page, the subscriber dashboard and the
        // subscription indexes all have the same data the Payment Elements flow would have produced.
        var stripeMetadata = subscriptionSession.GetOrCreate<StripeMetadata>();
        stripeMetadata.CustomerId = details.CustomerId;
        stripeMetadata.Subscriptions ??= [];
        stripeMetadata.Subscriptions[details.SubscriptionId] = new StripeSubscriptionMetadata
        {
            SubscriptionId = details.SubscriptionId,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };
        subscriptionSession.Put(stripeMetadata);

        subscriptionSession.Put(new SubscriptionsMetadata
        {
            Subscriptions =
            [
                new SubscriptionInfo
                {
                    SubscriptionId = details.SubscriptionId,
                    StartedAt = now,
                    ExpiresAt = expiresAt,
                    Gateway = StripeConstants.ProcessorKey,
                    GatewayMode = gatewayMode,
                    GatewayCustomerId = details.CustomerId,
                    LineItems = group.Value.ToList(),
                },
            ],
        });

        // Populate the payment session so the completing handler can validate the payment immediately,
        // without depending on the (eventually-consistent) invoice webhook having already arrived.
        await _subscriptionPaymentSession.SetAsync(subscriptionSession.SessionId, new SubscriptionPaymentsMetadata
        {
            Payments = new Dictionary<string, PaymentInfo>
            {
                [details.SubscriptionId] = new PaymentInfo
                {
                    TransactionId = details.SubscriptionId,
                    SubscriptionId = details.SubscriptionId,
                    Amount = details.AmountTotal,
                    Currency = invoice.Currency,
                    GatewayId = StripeConstants.ProcessorKey,
                    GatewayMode = gatewayMode,
                    Status = PaymentStatus.Succeeded,
                },
            },
        });

        await _subscriptionSessionStore.SaveAsync(subscriptionSession);

        if (await TryCompleteFlowAsync(flow, subscriptionSession, now))
        {
            new SubscriptionCookieManager(HttpContext).Remove(subscriptionContentItem.ContentItemId);

            return RedirectToAction(nameof(Confirmation), new
            {
                sessionId = subscriptionSession.SessionId,
            });
        }

        return RedirectToAction(nameof(Display), new
        {
            sessionId = subscriptionSession.SessionId,
            step = SubscriptionConstants.StepKey.Payment,
        });
    }

    private async Task<ContentItem> GetSubscriptionVersion(string versionContentItemId)
        => await _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.ContentItemVersionId == versionContentItemId)
        .FirstOrDefaultAsync();
}
