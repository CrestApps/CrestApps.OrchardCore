using System.Security.Claims;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.AgenticCommerce.ViewModels;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Throttling;
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
    private readonly IAttemptLimiter _limiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentHandoffController"/> class.
    /// </summary>
    /// <param name="urls">The agent addresses.</param>
    /// <param name="checkouts">The agent checkout store.</param>
    /// <param name="sessions">The store checkout store.</param>
    /// <param name="guestTokens">The guest session tokens.</param>
    /// <param name="engine">The checkout engine.</param>
    /// <param name="limiter">The attempt limiter, which limits how often an anonymous link asks the provider.</param>
    public AgentHandoffController(
        AgentSiteUrls urls,
        IAgentCheckoutStore checkouts,
        ICheckoutSessionStore sessions,
        GuestSessionTokenManager guestTokens,
        ICheckoutEngine engine,
        IAttemptLimiter limiter)
    {
        _limiter = limiter;
        _urls = urls;
        _checkouts = checkouts;
        _sessions = sessions;
        _guestTokens = guestTokens;
        _engine = engine;
    }

    /// <summary>
    /// Opens the checkout's next step in the browser it was given to. A checkout no browser was given yet is not given on
    /// a GET, which a link preview or a mail scanner sends on its own: the page asks the buyer to continue, and that post
    /// gives it (<see cref="ContinuePost"/>). Opened with <c>ec_version</c> by a platform that may embed it, shows the UCP
    /// embedded checkout page.
    /// </summary>
    /// <param name="token">The signed hand-off token.</param>
    [AllowAnonymous]
    [HttpGet(AgentSiteUrls.HandoffPath + "/{token}", Name = "AgentCheckoutHandoff")]
    public Task<IActionResult> Continue(string token)
        => OpenAsync(token, claim: false);

    /// <summary>
    /// Gives the browser the checkout when no browser was given it yet, then opens its next step.
    /// </summary>
    /// <param name="token">The signed hand-off token.</param>
    [AllowAnonymous]
    [HttpPost(AgentSiteUrls.HandoffPath + "/{token}")]
    [ActionName(nameof(Continue))]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ContinuePost(string token)
        => OpenAsync(token, claim: true);

    private async Task<IActionResult> OpenAsync(string token, bool claim)
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
        // the signed-in user, or to a guest who holds its token. It is given once: the link opened again, by anyone who
        // got hold of it, doesn't hand the checkout over a second time. A checkout made for a linked buyer stays theirs.
        var claimed = false;
        var claimable = (string.IsNullOrEmpty(session.OwnerId) && string.IsNullOrEmpty(session.GuestTokenHash)) ||
            session.OwnerId?.StartsWith(AgentPrincipal.OwnerPrefix, StringComparison.Ordinal) == true;

        // Given only by the buyer's own post, so a link preview that fetches the link first doesn't take the checkout. A
        // platform embedding the checkout opens it in its own frame, where the signed link authorizes it, as before.
        if (claimable && !claim && embedded is null)
        {
            return View("Claim");
        }

        if (claimable)
        {
            var userId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            session.OwnerId = userId;
            session.GuestTokenHash = userId is null ? _guestTokens.Issue(CheckoutSessionStore.GuestTokenScope, session.SessionId) : null;

            await _sessions.SaveAsync(session, HttpContext.RequestAborted);

            claimed = true;
        }

        // Opened again in a browser the checkout wasn't given to, such as the buyer's phone after they paid on their laptop,
        // the checkout and its confirmation would answer 404 there. That browser is told where the order stands and how to
        // see it, and nothing of the order itself: whoever holds the link could be reading.
        var owned = claimed || await _sessions.GetAsync(session.SessionId, session.Status, HttpContext.RequestAborted) is not null;

        if (!owned)
        {
            // Completing is idempotent and shows nothing, so a buyer back from their bank in another browser is told the
            // order was placed rather than that it is still open.
            var placed = session.Status == CheckoutSessionStatus.Completed ||
                (session.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending &&
                await TryCompleteAsync(session.SessionId));

            return View("Elsewhere", new AgentHandoffElsewhereViewModel { Placed = placed });
        }

        if (embedded is not null && await embedded.StartAsync(HttpContext, checkout, session, token) is { } model)
        {
            return View("EmbeddedCheckout", model);
        }

        // A buyer back from their bank's approval may find the payment settled while the provider's confirmation is still
        // on its way: the checkout is completed now, from the provider's answer, so they see their confirmation rather
        // than the checkout's first step. Completing is idempotent, so a webhook arriving at the same time pays nothing twice.
        if (session.Status is CheckoutSessionStatus.AwaitingProvider or CheckoutSessionStatus.PaymentPending &&
            await TryCompleteAsync(session.SessionId))
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId });
        }

        // A checkout already paid shows its confirmation, which is the buyer's now too.
        return session.Status == CheckoutSessionStatus.Completed
            ? RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId })
            : RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });
    }

    // Completing asks the payment provider, and the link is anonymous: whoever holds it is held to the limit a checkout's
    // status polls have, counted per checkout, and past it the page shows the checkout as it stands.
    private async Task<bool> TryCompleteAsync(string sessionId)
        => (await _limiter.AcquireAsync(CheckoutConstants.AttemptPolicies.PaymentStatus, "agent-handoff:" + sessionId, HttpContext.RequestAborted)).Allowed &&
            (await _engine.TryCompleteAsync(sessionId, HttpContext.RequestAborted)).IsCompleted;
}
