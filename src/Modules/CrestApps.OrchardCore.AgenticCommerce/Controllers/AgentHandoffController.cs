using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentHandoffController"/> class.
    /// </summary>
    /// <param name="urls">The agent addresses.</param>
    /// <param name="checkouts">The agent checkout store.</param>
    /// <param name="sessions">The store checkout store.</param>
    /// <param name="guestTokens">The guest session tokens.</param>
    public AgentHandoffController(
        AgentSiteUrls urls,
        IAgentCheckoutStore checkouts,
        ICheckoutSessionStore sessions,
        GuestSessionTokenManager guestTokens)
    {
        _urls = urls;
        _checkouts = checkouts;
        _sessions = sessions;
        _guestTokens = guestTokens;
    }

    /// <summary>
    /// Gives the browser the checkout, then opens its next step.
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

        if (session.Status == CheckoutSessionStatus.Completed)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId });
        }

        // A guest's checkout belongs to whoever holds its token: the browser the agent sends the buyer to gets one. A
        // signed-in buyer's checkout stays theirs.
        if (string.IsNullOrEmpty(session.OwnerId) || session.OwnerId.StartsWith(AgentPrincipal.OwnerPrefix, StringComparison.Ordinal))
        {
            session.OwnerId = null;
            session.GuestTokenHash = _guestTokens.Issue(CheckoutSessionStore.GuestTokenScope, session.SessionId);

            await _sessions.SaveAsync(session, HttpContext.RequestAborted);
        }

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });
    }
}
