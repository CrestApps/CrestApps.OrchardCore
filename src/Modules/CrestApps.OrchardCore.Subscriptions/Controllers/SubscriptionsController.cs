using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.RateLimits;

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
    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICheckoutEngine _engine;
    private readonly IPriceResolver _priceResolver;
    private readonly IShapeFactory _shapeFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to load the published plan.</param>
    /// <param name="contentDefinitionManager">The content definition manager used to check the plan is a subscription.</param>
    /// <param name="engine">The checkout engine that owns the purchase.</param>
    /// <param name="priceResolver">The pricing seam that decides whether the plan can be sold.</param>
    /// <param name="shapeFactory">The shape factory that builds the page shown when the plan can't be bought.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionsController(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ICheckoutEngine engine,
        IPriceResolver priceResolver,
        IShapeFactory shapeFactory,
        ILogger<SubscriptionsController> logger)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
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

        var plan = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (plan is null || !plan.TryGet<SubscriptionPart>(out _) || !await IsSubscriptionTypeAsync(plan.ContentType))
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

    // Only a plan of a subscription content type is sold here; a type without a definition is let through, as the
    // part alone makes it one.
    private async Task<bool> IsSubscriptionTypeAsync(string contentType)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return definition is null || definition.StereotypeEquals(SubscriptionConstants.Stereotype);
    }
}
