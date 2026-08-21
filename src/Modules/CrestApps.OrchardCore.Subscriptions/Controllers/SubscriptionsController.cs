using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Wizard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Preserves the subscription-specific public routes while delegating the multi-step signup experience to
/// the shared wizard controller and engine.
/// </summary>
public sealed class SubscriptionsController : Controller
{
    private readonly ISession _session;
    private readonly ISubscriptionSessionStore _subscriptionSessionStore;
    private readonly SubscriptionPaymentSession _subscriptionPaymentSession;
    private readonly IDistributedLock _distributedLock;
    private readonly IWizardEngine _wizardEngine;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger<SubscriptionsController> _logger;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query subscription content items.</param>
    /// <param name="subscriptionSessionStore">The store used to load subscription checkout sessions.</param>
    /// <param name="subscriptionPaymentSession">The payment session store used during checkout completion.</param>
    /// <param name="distributedLock">The distributed lock used to serialize hosted checkout returns.</param>
    /// <param name="wizardEngine">The shared wizard engine used to finalize the checkout.</param>
    /// <param name="notifier">The notifier used to show checkout messages.</param>
    /// <param name="clock">The clock used to assign Stripe-derived timestamps.</param>
    /// <param name="logger">The logger used to record checkout errors.</param>
    /// <param name="htmlLocalizer">The HTML localizer for subscription flow messages.</param>
    public SubscriptionsController(
        ISession session,
        ISubscriptionSessionStore subscriptionSessionStore,
        SubscriptionPaymentSession subscriptionPaymentSession,
        IDistributedLock distributedLock,
        IWizardEngine wizardEngine,
        INotifier notifier,
        IClock clock,
        ILogger<SubscriptionsController> logger,
        IHtmlLocalizer<SubscriptionsController> htmlLocalizer)
    {
        _session = session;
        _subscriptionSessionStore = subscriptionSessionStore;
        _subscriptionPaymentSession = subscriptionPaymentSession;
        _distributedLock = distributedLock;
        _wizardEngine = wizardEngine;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Starts or resumes the wizard-backed signup session for a published subscription service plan.
    /// </summary>
    /// <param name="contentItemId">The content item identifier of the published subscription service plan.</param>
    /// <returns>A redirect to the shared wizard route, or a not found result when the service plan cannot be loaded.</returns>
    [HttpGet("Subscription/Signup/{contentItemId}", Name = "SubscriptionSignup")]
    public async Task<IActionResult> Signup(string contentItemId)
    {
        var subscriptionContentItem = await GetPublishedSubscriptionAsync(contentItemId);

        if (subscriptionContentItem == null)
        {
            return NotFound();
        }

        var legacyCookieManager = new SubscriptionCookieManager(HttpContext);

        if (legacyCookieManager.TryGetValue(contentItemId, out var existingSessionId) &&
            !string.IsNullOrEmpty(existingSessionId))
        {
            var subscriptionSession = await _subscriptionSessionStore.GetAsync(existingSessionId, SubscriptionSessionStatus.Pending);

            if (subscriptionSession != null &&
                string.Equals(subscriptionSession.ContentItemVersionId, subscriptionContentItem.ContentItemVersionId, StringComparison.Ordinal))
            {
                return RedirectToRoute(WizardConstants.RouteNames.Step, new
                {
                    sessionId = subscriptionSession.SessionId,
                    step = subscriptionSession.CurrentStep,
                });
            }
        }

        return RedirectToRoute(WizardConstants.RouteNames.Start, new
        {
            wizardType = SubscriptionConstants.WizardType,
            definitionId = contentItemId,
        });
    }

    /// <summary>
    /// Redirects stale compatibility posts back into the shared wizard host so a visitor can continue on the
    /// shared wizard route after a deployment.
    /// </summary>
    /// <param name="model">The posted subscription view model.</param>
    /// <returns>A redirect to the shared wizard host.</returns>
    [HttpPost("Subscription/Signup/{contentItemId?}")]
    [ActionName(nameof(Signup))]
    [RateLimitGroup(SubscriptionConstants.RateLimitGroups.Checkout)]
    [ValidateAntiForgeryToken]
    public IActionResult SignupPOST(ServicePlanSubscriptionViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model?.SessionId))
        {
            return RedirectToRoute(WizardConstants.RouteNames.Step, new
            {
                sessionId = model.SessionId,
                step = model.Step,
            });
        }

        return RedirectToRoute(WizardConstants.RouteNames.Start, new
        {
            wizardType = SubscriptionConstants.WizardType,
            definitionId = model?.ContentItemId,
        });
    }

    /// <summary>
    /// Redirects the legacy step route to the shared wizard step route.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="step">The optional saved step key to display.</param>
    /// <returns>A redirect to the shared wizard step route.</returns>
    [Route("Subscription/Step/{sessionId}", Name = "SubscriptionSignupStep")]
    [RateLimitGroup(SubscriptionConstants.RateLimitGroups.Checkout)]
    public IActionResult Display(string sessionId, string step)
        => RedirectToRoute(WizardConstants.RouteNames.Step, new
        {
            sessionId,
            step,
        });

    /// <summary>
    /// Redirects the legacy confirmation route to the shared wizard confirmation route.
    /// </summary>
    /// <param name="sessionId">The completed subscription session identifier.</param>
    /// <returns>A redirect to the shared wizard confirmation route.</returns>
    [Route("Subscription/Confirmation/{sessionId}", Name = "SubscriptionConfirmation")]
    public IActionResult Confirmation(string sessionId)
        => RedirectToRoute(WizardConstants.RouteNames.Confirmation, new
        {
            sessionId,
        });

    /// <summary>
    /// The URL Stripe redirects to after a customer completes a hosted Stripe Checkout.
    /// </summary>
    /// <param name="sessionId">The local subscription session identifier.</param>
    /// <param name="checkoutSessionId">The Stripe Checkout session identifier returned by Stripe.</param>
    /// <returns>A redirect to confirmation, a redirect back to the payment step, or a not found result.</returns>
    [Route("Subscription/CheckoutReturn/{sessionId}", Name = "SubscriptionCheckoutReturn")]
    public async Task<IActionResult> CheckoutReturn(string sessionId, string checkoutSessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return NotFound();
        }

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
        var subscriptionSession = await _subscriptionSessionStore.GetAsync(sessionId, SubscriptionSessionStatus.Pending);

        if (subscriptionSession == null)
        {
            return NotFound();
        }

        var subscriptionContentItem = await GetSubscriptionVersionAsync(subscriptionSession.ContentItemVersionId);

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

        var validation = HostedCheckoutReturnValidator.Validate(details, subscriptionSession.SessionId, invoice?.Currency);

        if (validation != CheckoutReturnValidation.Valid || invoice == null)
        {
            if (validation == CheckoutReturnValidation.CurrencyMismatch)
            {
                _logger.LogWarning(
                    "Checkout return for session '{SessionId}' rejected: Stripe currency '{StripeCurrency}' does not match invoice currency '{InvoiceCurrency}'.",
                    subscriptionSession.SessionId,
                    details.Currency,
                    invoice?.Currency);
            }

            await _notifier.ErrorAsync(H["Your payment could not be confirmed. Please try again."]);

            return RedirectToAction(nameof(Display), new
            {
                sessionId = subscriptionSession.SessionId,
                step = SubscriptionConstants.StepKey.Payment,
            });
        }

        var groups = invoice.GetSubscriptionGroups();

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

        subscriptionSession.ModifiedUtc = now;

        await _subscriptionSessionStore.SaveAsync(subscriptionSession);
        await _session.SaveChangesAsync();

        var result = await _wizardEngine.CompleteAsync(new WizardFlow(new WizardSession
        {
            SessionId = subscriptionSession.SessionId,
            WizardType = SubscriptionConstants.WizardType,
        }));

        if (result.IsCompleted)
        {
            new SubscriptionCookieManager(HttpContext).Remove(subscriptionContentItem.ContentItemId);

            return RedirectToAction(nameof(Confirmation), new
            {
                sessionId = subscriptionSession.SessionId,
            });
        }

        if (result.Status == WizardCompletionStatus.Blocked && !string.IsNullOrEmpty(result.BlockingStepKey))
        {
            return RedirectToAction(nameof(Display), new
            {
                sessionId = subscriptionSession.SessionId,
                step = result.BlockingStepKey,
            });
        }

        return RedirectToAction(nameof(Display), new
        {
            sessionId = subscriptionSession.SessionId,
            step = SubscriptionConstants.StepKey.Payment,
        });
    }

    private Task<ContentItem> GetPublishedSubscriptionAsync(string contentItemId)
        => _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.Published && index.ContentItemId == contentItemId)
            .FirstOrDefaultAsync();

    private Task<ContentItem> GetSubscriptionVersionAsync(string versionContentItemId)
        => _session.Query<ContentItem, SubscriptionsContentItemIndex>(index => index.ContentItemVersionId == versionContentItemId)
            .FirstOrDefaultAsync();
}
