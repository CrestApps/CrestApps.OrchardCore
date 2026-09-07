using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Checkout.Controllers;

/// <summary>
/// The public checkout experience: it walks a customer through the steps a checkout contributed, and hands
/// the money-moving parts to <see cref="ICheckoutEngine"/>.
/// </summary>
/// <remarks>
/// The controller deliberately contains no payment logic. It renders steps, saves what the customer typed,
/// and asks the engine to complete; deciding whether a payment really settled belongs in one place that every
/// entry point (this page, a provider webhook, the reconciliation sweep) shares.
/// </remarks>
public sealed class CheckoutController : Controller
{
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly ICheckoutEngine _engine;
    private readonly IDisplayManager<CheckoutFlow> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutController"/> class.
    /// </summary>
    /// <param name="sessionStore">The checkout session store, which enforces session ownership.</param>
    /// <param name="engine">The checkout engine that owns every money-moving transition.</param>
    /// <param name="displayManager">The display manager that renders the flow's steps.</param>
    /// <param name="updateModelAccessor">The accessor supplying the current model updater.</param>
    /// <param name="notifier">The notifier used to surface checkout messages.</param>
    /// <param name="clock">The clock used to stamp session changes.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CheckoutController(
        ICheckoutSessionStore sessionStore,
        ICheckoutEngine engine,
        IDisplayManager<CheckoutFlow> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IClock clock,
        ILogger<CheckoutController> logger,
        IHtmlLocalizer<CheckoutController> htmlLocalizer)
    {
        _sessionStore = sessionStore;
        _engine = engine;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Displays a step of a pending checkout.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="step">The step to display; defaults to the session's current step.</param>
    [HttpGet("Checkout/{sessionId}/{step?}", Name = CheckoutConstants.RouteNames.Step)]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> Display(string sessionId, string step)
    {
        // The store only returns a pending session that belongs to this caller, which is what stops one
        // visitor resuming another's checkout.
        var session = await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Pending);

        if (session is null)
        {
            return NotFound();
        }

        var flow = new CheckoutFlow(session);

        // A customer may only jump back to a step they already completed. Letting them jump forward would
        // reach payment with earlier steps unfilled.
        if (!string.IsNullOrEmpty(step) && session.SavedSteps.ContainsKey(step))
        {
            flow.SetCurrentStep(step);
        }

        var content = await _displayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, isNew: false);

        return View(new CheckoutViewModel
        {
            SessionId = session.SessionId,
            Step = flow.GetCurrentStep()?.Key,
            Content = content,
        });
    }

    /// <summary>
    /// Saves the posted step and advances, or completes the checkout when the last step is submitted.
    /// </summary>
    /// <param name="model">The posted checkout model.</param>
    [HttpPost("Checkout/{sessionId}/{step?}")]
    [ActionName(nameof(Display))]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CheckoutConstants.RateLimitGroups.Checkout)]
    public async Task<IActionResult> DisplayPost(CheckoutViewModel model)
    {
        var session = await _sessionStore.GetAsync(model?.SessionId, CheckoutSessionStatus.Pending);

        if (session is null)
        {
            return NotFound();
        }

        var flow = new CheckoutFlow(session);

        if (!string.IsNullOrEmpty(model.Step) && session.SavedSteps.ContainsKey(model.Step))
        {
            flow.SetCurrentStep(model.Step);
        }

        var content = await _displayManager.UpdateEditorAsync(flow, _updateModelAccessor.ModelUpdater, isNew: false);

        if (!_updateModelAccessor.ModelUpdater.ModelState.IsValid)
        {
            return View(nameof(Display), new CheckoutViewModel
            {
                SessionId = session.SessionId,
                Step = flow.GetCurrentStep()?.Key,
                Content = content,
            });
        }

        session.ModifiedUtc = _clock.UtcNow;

        var nextStep = flow.GetNextStep();

        if (nextStep is not null)
        {
            flow.SetCurrentStep(nextStep.Key);

            await _sessionStore.SaveAsync(session);

            return RedirectToRoute(CheckoutConstants.RouteNames.Step, new
            {
                sessionId = session.SessionId,
                step = nextStep.Key,
            });
        }

        await _sessionStore.SaveAsync(session);

        return await CompleteAsync(session.SessionId);
    }

    /// <summary>
    /// Displays the confirmation of a completed checkout.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    [HttpGet("Checkout/Confirmation/{sessionId}", Name = CheckoutConstants.RouteNames.Confirmation)]
    public async Task<IActionResult> Confirmation(string sessionId)
    {
        var session = await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Completed);

        if (session is null)
        {
            return NotFound();
        }

        var flow = new CheckoutFlow(session);

        var content = await _displayManager.BuildDisplayAsync(flow, _updateModelAccessor.ModelUpdater, "Confirmation");

        return View(new CheckoutViewModel
        {
            SessionId = session.SessionId,
            Content = content,
        });
    }

    // Turns an engine outcome into the right place to send the customer. Each outcome gets its own
    // destination because they mean different things: pending is "wait", blocked is "you missed something",
    // and failed is "this did not happen".
    private async Task<IActionResult> CompleteAsync(string sessionId)
    {
        CheckoutCompletionResult result;

        try
        {
            result = await _engine.TryCompleteAsync(sessionId);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Completing checkout '{SessionId}' threw.", sessionId);

            await _notifier.ErrorAsync(H["We could not complete your purchase. Please try again, or contact us if the problem continues."]);

            return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId, step = CheckoutConstants.PaymentStepKey });
        }

        if (result.IsCompleted)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId });
        }

        switch (result.Status)
        {
            case CheckoutCompletionStatus.Blocked:
                return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId, step = result.BlockingStepKey });

            case CheckoutCompletionStatus.Pending:
                // The provider has not confirmed yet. The customer is told to wait rather than shown a
                // failure, and the reconciliation sweep finishes the job if they close the browser.
                await _notifier.InformationAsync(H["Your payment is still being processed. This page will update once your provider confirms it."]);

                break;

            case CheckoutCompletionStatus.NotFound:
                return NotFound();

            default:
                await _notifier.ErrorAsync(H["{0}", result.ErrorMessage ?? H["Your payment could not be completed."].Value]);

                break;
        }

        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId, step = CheckoutConstants.PaymentStepKey });
    }
}
