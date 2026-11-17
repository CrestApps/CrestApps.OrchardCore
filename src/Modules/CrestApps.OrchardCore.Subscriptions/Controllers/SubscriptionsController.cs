using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Throttling;
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
/// Subscriptions do not own a checkout of their own. This controller resolves the plan the visitor chose,
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
    private readonly IAttemptLimiter _limiter;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to load the published plan.</param>
    /// <param name="contentDefinitionManager">The content definition manager used to check the plan is a subscription.</param>
    /// <param name="engine">The checkout engine that owns the purchase.</param>
    /// <param name="priceResolver">The pricing seam that decides whether the plan can be sold.</param>
    /// <param name="shapeFactory">The shape factory that builds the page shown when the plan can't be bought.</param>
    /// <param name="limiter">The attempt limiter that limits the checkouts one network starts.</param>
    /// <param name="logger">The logger.</param>
    public SubscriptionsController(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ICheckoutEngine engine,
        IPriceResolver priceResolver,
        IShapeFactory shapeFactory,
        IAttemptLimiter limiter,
        ILogger<SubscriptionsController> logger)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _engine = engine;
        _priceResolver = priceResolver;
        _shapeFactory = shapeFactory;
        _limiter = limiter;
        _logger = logger;
    }

    /// <summary>
    /// Shows a published subscription plan with the button that starts its checkout.
    /// </summary>
    /// <remarks>
    /// A link is followed by more than the buyer: link scanners, mail previews and crawlers open it too. So a link only
    /// shows the plan, and the button's post starts the checkout. Links in e-mails sent before sign-up became a post land
    /// here and still work: the buyer presses the button.
    /// </remarks>
    /// <param name="contentItemId">The content item identifier of the published plan.</param>
    /// <param name="priceId">The price the buyer chose, if the plan offers more than one.</param>
    /// <param name="quantity">How many the buyer is taking.</param>
    /// <param name="customAmount">The amount the buyer named, for a price that lets them name one.</param>
    /// <returns>The page with the button, or a not found result when the plan cannot be bought.</returns>
    [HttpGet("Subscription/Signup/{contentItemId}", Name = SubscriptionConstants.RouteName.Signup)]
    public async Task<IActionResult> Signup(string contentItemId, string priceId = null, int quantity = 1, decimal? customAmount = null)
    {
        var (plan, selection, pricing) = await ResolveAsync(contentItemId, priceId, quantity, customAmount);

        if (plan is null)
        {
            return NotFound();
        }

        if (!pricing.CanBeBought)
        {
            return await UnavailableAsync(plan, pricing);
        }

        // A named shape, so a theme can replace the page's content with its own SubscriptionSignupConfirm template.
        return View(await _shapeFactory.CreateAsync<SubscriptionSignupConfirmViewModel>("SubscriptionSignupConfirm", model =>
        {
            model.ContentItemId = plan.ContentItemId;
            model.Title = plan.DisplayText;
            model.PriceId = selection.PriceId;
            model.Quantity = selection.Quantity;
            model.CustomAmount = selection.CustomAmount;
            model.Amount = pricing.Price.Subtotal;
            model.Currency = pricing.Price.Currency;
            model.BillingDuration = pricing.Recurrence.BillingDuration;
            model.DurationType = pricing.Recurrence.DurationType;
        }));
    }

    /// <summary>
    /// Starts a checkout for a published subscription service plan, from the plan's sign-up button.
    /// </summary>
    /// <param name="contentItemId">The content item identifier of the published plan.</param>
    /// <param name="priceId">The price the buyer chose, if the plan offers more than one.</param>
    /// <param name="quantity">How many the buyer is taking.</param>
    /// <param name="customAmount">The amount the buyer named, for a price that lets them name one.</param>
    /// <returns>A redirect into the checkout, or a not found result when the plan cannot be bought.</returns>
    [HttpPost("Subscription/Signup/{contentItemId}")]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Start)]
    public async Task<IActionResult> SignupPost(string contentItemId, string priceId = null, int quantity = 1, decimal? customAmount = null)
    {
        var (plan, selection, pricing) = await ResolveAsync(contentItemId, priceId, quantity, customAmount);

        if (plan is null)
        {
            return NotFound();
        }

        if (!pricing.CanBeBought)
        {
            return await UnavailableAsync(plan, pricing);
        }

        // Every press starts a checkout, so what keeps a script from piling them up is a limit per network, the same one
        // an order's pay link takes.
        if (await _limiter.AcquireAsync(CheckoutConstants.AttemptPolicies.CheckoutStartNetwork, ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted) is { Allowed: false } refused)
        {
            await ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(HttpContext, refused.RetryAfter, HttpContext.RequestAborted);

            return new EmptyResult();
        }

        var session = await _engine.StartAsync(new StartCheckoutRequest
        {
            ReferenceType = SubscriptionCheckout.ReferenceType,
            ReferenceId = plan.ContentItemId,
            ReferenceVersionId = plan.ContentItemVersionId,
            PriceSelection = selection,
        }, HttpContext.RequestAborted);

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
        {
            sessionId = session.SessionId,
            step = session.CurrentStep,
        });
    }

    // The plan, what the buyer chose from it, and its price; no plan when it isn't a published subscription plan.
    private async Task<(ContentItem Plan, CheckoutPriceSelection Selection, SubscriptionPlanPrice Pricing)> ResolveAsync(string contentItemId, string priceId, int quantity, decimal? customAmount)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return default;
        }

        var plan = await _contentManager.GetAsync(contentItemId, VersionOptions.Published);

        if (plan is null || !plan.TryGet<SubscriptionPart>(out _) || !await IsSubscriptionTypeAsync(plan.ContentType))
        {
            return default;
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
        return (plan, selection, await SubscriptionPlanPricing.ResolveAsync(_priceResolver, plan, selection));
    }

    private async Task<IActionResult> UnavailableAsync(ContentItem plan, SubscriptionPlanPrice pricing)
    {
        _logger.LogWarning(
            "Subscription plan '{ContentItemId}' ({Title}) can't be bought: {Problem}. Give it a price in a currency (or set a default currency on its content type), and a price that recurs.",
            plan.ContentItemId,
            plan.DisplayText,
            pricing.Problem);

        return View("Unavailable", await _shapeFactory.CreateAsync("SubscriptionUnavailable"));
    }

    // Only a plan of a subscription content type is sold here; a type without a definition is let through, as the
    // part alone makes it one.
    private async Task<bool> IsSubscriptionTypeAsync(string contentType)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return definition is null || definition.StereotypeEquals(SubscriptionConstants.Stereotype);
    }
}
