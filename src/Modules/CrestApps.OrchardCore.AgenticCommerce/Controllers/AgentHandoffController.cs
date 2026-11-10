using System.Security.Claims;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.Controllers;

/// <summary>
/// Opens an agent checkout in the store's own checkout, in the buyer's browser, when the agent can't finish it: the
/// buyer approves a payment their bank asked about, or gives details the agent can't.
/// </summary>
public sealed class AgentHandoffController : Controller
{
    private readonly AgentSiteUrls _urls;
    private readonly IAgentCheckoutStore _checkouts;
    private readonly ICheckoutSessionStore _sessions;
    private readonly GuestSessionTokenManager _guestTokens;
    private readonly ICheckoutEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentHandoffController"/> class.
    /// </summary>
    /// <param name="urls">The agent addresses.</param>
    /// <param name="checkouts">The agent checkout store.</param>
    /// <param name="sessions">The store checkout store.</param>
    /// <param name="guestTokens">The guest session tokens.</param>
    /// <param name="engine">The checkout engine.</param>
    public AgentHandoffController(
        AgentSiteUrls urls,
        IAgentCheckoutStore checkouts,
        ICheckoutSessionStore sessions,
        GuestSessionTokenManager guestTokens,
        ICheckoutEngine engine)
    {
        _urls = urls;
        _checkouts = checkouts;
        _sessions = sessions;
        _guestTokens = guestTokens;
        _engine = engine;
    }

    /// <summary>
    /// Gives the browser the checkout, then opens its next step; or, opened with <c>ec_version</c> by a platform that may
    /// embed it, shows the UCP embedded checkout page.
    /// </summary>
    /// <param name="token">The signed hand-off token.</param>
    [AllowAnonymous]
    [HttpGet(AgentSiteUrls.HandoffPath + "/{token}", Name = "AgentCheckoutHandoff")]
    public async Task<IActionResult> Continue(string token)
    {
        if (_urls.ReadHandoffToken(token) is not { } checkoutId ||
            await _checkouts.FindByIdAsync(checkoutId, HttpContext.RequestAborted) is not { } checkout ||
            string.IsNullOrEmpty(checkout.CheckoutSessionId) ||
            await _sessions.GetAsync(checkout.CheckoutSessionId, HttpContext.RequestAborted) is not { } session)
        {
            return NotFound();
        }

        // A platform that opens the link with ec_version shows the checkout inside its own page (UCP embedded checkout).
        // When the checkout can't be embedded, the buyer gets the ordinary hand-off.
        var embedded = Request.Query.ContainsKey(UcpConstants.Embedded.VersionParameter)
            ? HttpContext.RequestServices.GetService<EmbeddedCheckoutService>()
            : null;

        // The checkout goes to the browser the agent sends the buyer to, the way the store gives out a new checkout: to
        // the signed-in user, or to a guest who holds its token. A checkout already made for a linked buyer stays theirs.
        if (string.IsNullOrEmpty(session.OwnerId) || session.OwnerId.StartsWith(AgentPrincipal.OwnerPrefix, StringComparison.Ordinal))
        {
            var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            session.OwnerId = userId;
            session.GuestTokenHash = userId is null ? _guestTokens.Issue(CheckoutSessionStore.GuestTokenScope, session.SessionId) : null;

            await _sessions.SaveAsync(session, HttpContext.RequestAborted);
        }

        if (embedded is not null && await embedded.StartAsync(HttpContext, checkout, session, token) is { } model)
        {
            return View("EmbeddedCheckout", model);
        }

        // A buyer back from their bank's approval may find the payment settled while the provider's confirmation is still
        // on its way: the checkout is completed now, from the provider's answer, so they see their confirmation rather
        // than the checkout's first step. Completing is idempotent, so a webhook arriving at the same time pays nothing twice.
        if (session.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending &&
            (await _engine.TryCompleteAsync(session.SessionId, HttpContext.RequestAborted)).IsCompleted)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId });
        }

        // A checkout already paid shows its confirmation, which is the buyer's now too.
        return session.Status == CheckoutSessionStatus.Completed
            ? RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId })
            : RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });
    }
}
