using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement;
using OrchardCore.RateLimits;
using YesSql;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// The public entry point for buying a subscription plan.
/// </summary>
/// <remarks>
/// Subscriptions do not own a checkout of their own. This controller resolves the plan the visitor clicked,
/// starts a checkout that references it, and hands the visitor to the shared checkout. Everything after that
/// — steps, payment, completion, the durable agreement — belongs to the checkout framework, so a subscription
/// purchase and any other purchase settle through exactly one code path.
/// </remarks>
public sealed class SubscriptionsController : Controller
{
    private readonly ISession _session;
    private readonly ICheckoutEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to resolve the published plan.</param>
    /// <param name="engine">The checkout engine that owns the purchase.</param>
    public SubscriptionsController(
        ISession session,
        ICheckoutEngine engine)
    {
        _session = session;
        _engine = engine;
    }

    /// <summary>
    /// Starts a checkout for a published subscription service plan.
    /// </summary>
    /// <param name="contentItemId">The content item identifier of the published plan.</param>
    /// <returns>A redirect into the checkout, or a not found result when the plan cannot be bought.</returns>
    [HttpGet("Subscription/Signup/{contentItemId}", Name = SubscriptionConstants.RouteName.Signup)]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> Signup(string contentItemId)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return NotFound();
        }

        var plan = await _session
            .Query<ContentItem, SubscriptionsContentItemIndex>(index => index.Published && index.ContentItemId == contentItemId)
            .FirstOrDefaultAsync();

        if (plan is null)
        {
            return NotFound();
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = SubscriptionCheckout.ReferenceType,
            ReferenceId = plan.ContentItemId,
            ReferenceVersionId = plan.ContentItemVersionId,
        });

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }
}
