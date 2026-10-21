using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
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
    private readonly IPriceResolver _priceResolver;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to resolve the published plan.</param>
    /// <param name="engine">The checkout engine that owns the purchase.</param>
    /// <param name="priceResolver">The pricing seam that decides whether the plan can be sold.</param>
    /// <param name="shapeFactory">The shape factory that builds the page shown when the plan can't be bought.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionsController(
        ISession session,
        ICheckoutEngine engine,
        IPriceResolver priceResolver,
        IShapeFactory shapeFactory,
        ILogger<SubscriptionsController> logger)
    {
        _session = session;
        _engine = engine;
        _priceResolver = priceResolver;
        _shapeFactory = shapeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts a checkout for a published subscription service plan.
    /// </summary>
    /// <param name="contentItemId">The content item identifier of the published plan.</param>
    /// <returns>A redirect into the checkout, or a not found result when the plan cannot be bought.</returns>
    [HttpGet("Subscription/Signup/{contentItemId}", Name = SubscriptionConstants.RouteName.Signup)]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> Signup(string contentItemId, string priceId = null, int quantity = 1, decimal? customAmount = null)
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

        // Carried, not trusted: the resolver refuses a price that is not on offer, an amount outside what the
        // price allows, and a quantity the price does not permit.
        var selection = new CheckoutPriceSelection
        {
            PriceId = priceId,
            Quantity = quantity,
            CustomAmount = customAmount,
        };

        // A plan that can't be priced as a subscription would start a checkout that bills nothing. Refusing it
        // here keeps the buyer from walking through the steps for something they can't buy.
        var pricing = await SubscriptionPlanPricing.ResolveAsync(_priceResolver, plan, selection);

        if (!pricing.CanBeBought)
        {
            _logger.LogWarning(
                "Subscription plan '{ContentItemId}' ({Title}) can't be bought: {Problem}. Give it a price in a currency (or set a default currency on its content type), and a price that recurs.",
                plan.ContentItemId,
                plan.DisplayText,
                pricing.Problem);

            return View("Unavailable", await _shapeFactory.CreateAsync("SubscriptionUnavailable"));
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = SubscriptionCheckout.ReferenceType,
            ReferenceId = plan.ContentItemId,
            ReferenceVersionId = plan.ContentItemVersionId,
            PriceSelection = selection,
        });

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }
}
