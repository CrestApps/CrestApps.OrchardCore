using CrestApps.OrchardCore.Checkout.Models;
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
using OrchardCore.Settings;

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
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;
    private readonly ISiteService _siteService;
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
    /// <param name="shapeFactory">The shape factory that builds the pages a theme can override.</param>
    /// <param name="notifier">The notifier used to surface checkout messages.</param>
    /// <param name="siteService">The site service, which holds how the checkout is laid out.</param>
    /// <param name="clock">The clock used to stamp session changes.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public CheckoutController(
        ICheckoutSessionStore sessionStore,
        ICheckoutEngine engine,
        IDisplayManager<CheckoutFlow> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IShapeFactory shapeFactory,
        INotifier notifier,
        ISiteService siteService,
        IClock clock,
        ILogger<CheckoutController> logger,
        IHtmlLocalizer<CheckoutController> htmlLocalizer)
    {
        _sessionStore = sessionStore;
        _engine = engine;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        _siteService = siteService;
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
        // The store only returns a live session that belongs to this caller, which is what stops one
        // visitor resuming another's checkout.
        var session = await GetLiveSessionAsync(sessionId);

        if (session is null)
        {
            return await ShowEndedAsync(sessionId);
        }

        var flow = new CheckoutFlow(session);

        if (await IsOnePageAsync())
        {
            return View("OnePage", await BuildOnePageAsync(flow, update: false, paymentShown: false));
        }

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
        var session = await GetLiveSessionAsync(model?.SessionId);

        if (session is null)
        {
            return await ShowEndedAsync(model?.SessionId);
        }

        var flow = new CheckoutFlow(session);

        if (await IsOnePageAsync())
        {
            return await OnePagePostAsync(flow, model);
        }

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

        // Some buttons on a step submit the form to change the step rather than to leave it: applying a
        // promotion code is one. Those save and re-render, because completing the checkout here would take
        // the customer's money the moment they asked to see a discount.
        if (Request.Form[CheckoutConstants.FormActions.FieldName] is { Count: > 0 } action &&
            (string.Equals(action, CheckoutConstants.FormActions.ApplyCoupon, StringComparison.Ordinal) ||
             string.Equals(action, CheckoutConstants.FormActions.Update, StringComparison.Ordinal)))
        {
            await _sessionStore.SaveAsync(session);

            return View(nameof(Display), new CheckoutViewModel
            {
                SessionId = session.SessionId,
                Step = flow.GetCurrentStep()?.Key,
                Content = await _displayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, isNew: false),
            });
        }

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
    /// <param name="attemptStore">The payment attempts, which say how the checkout was paid.</param>
    /// <param name="providers">The payment providers.</param>
    [HttpGet("Checkout/Confirmation/{sessionId}", Name = CheckoutConstants.RouteNames.Confirmation)]
    public async Task<IActionResult> Confirmation(
        string sessionId,
        [FromServices] CrestApps.OrchardCore.Transactions.Services.IPaymentAttemptStore attemptStore,
        [FromServices] ICheckoutPaymentProviderResolver providers)
    {
        var session = await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Completed);

        if (session is null)
        {
            return NotFound();
        }

        var flow = new CheckoutFlow(session);

        var content = await _displayManager.BuildDisplayAsync(flow, _updateModelAccessor.ModelUpdater, "Confirmation");

        var returnInfo = CheckoutReturnInfo.GetFrom(session);

        return View(new CheckoutViewModel
        {
            SessionId = session.SessionId,
            Content = content,
            ReturnUrl = returnInfo?.ReturnUrl,
            ReturnText = returnInfo?.ReturnText,
            Title = returnInfo?.Title,
            PaymentDeferred = (await attemptStore.GetBySessionAsync(session.SessionId))
                .Any(attempt => attempt.State == CrestApps.OrchardCore.Transactions.Models.PaymentAttemptState.Succeeded && providers.GetProvider(attempt.ProviderKey)?.Capabilities.DefersPayment == true),
        });
    }

    private async Task<bool> IsOnePageAsync()
        => (await _siteService.GetSettingsAsync<CheckoutSettings>()).Layout == CheckoutLayout.OnePage;

    // A one-page checkout saves every section it showed at once. Payment itself is taken by the payment script, so a
    // post here either saves the details and shows the page again, or, with payment already showing and nothing to
    // pay with a gateway, completes the checkout as the last step of the stepped layout does.
    private async Task<IActionResult> OnePagePostAsync(CheckoutFlow flow, CheckoutViewModel model)
    {
        var session = (CheckoutSession)flow.Session;
        var paymentShown = string.Equals(model.Step, CheckoutConstants.PaymentStepKey, StringComparison.Ordinal);
        var page = await BuildOnePageAsync(flow, update: true, paymentShown);

        if (!_updateModelAccessor.ModelUpdater.ModelState.IsValid)
        {
            page.NeedsUpdate = page.ShowsPayment;

            return View("OnePage", page);
        }

        session.ModifiedUtc = _clock.UtcNow;

        var action = Request.Form[CheckoutConstants.FormActions.FieldName].ToString();
        var completes = paymentShown && string.IsNullOrEmpty(action) && IsReadyToPay(flow);

        if (completes)
        {
            flow.SetCurrentStep(CheckoutConstants.PaymentStepKey);
        }

        await _sessionStore.SaveAsync(session);

        if (completes)
        {
            return await CompleteAsync(session.SessionId);
        }

        // The page is shown again from a fresh request, so a reload doesn't post the form a second time.
        return RedirectToRoute(CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });
    }

    // Builds a section per step, in order. The details are always on the page; payment joins them once every step
    // before it is complete, so the total it takes is the one the buyer sees.
    private async Task<CheckoutOnePageViewModel> BuildOnePageAsync(CheckoutFlow flow, bool update, bool paymentShown)
    {
        var session = flow.Session;
        var updater = _updateModelAccessor.ModelUpdater;
        var page = new CheckoutOnePageViewModel
        {
            SessionId = session.SessionId,
            Flow = flow,
        };

        foreach (var step in flow.GetSortedSteps())
        {
            if (IsPayment(step))
            {
                continue;
            }

            flow.SetCurrentStep(step.Key);

            page.Sections.Add(new CheckoutOnePageSection
            {
                Step = step,
                Shape = update
                    ? await _displayManager.UpdateEditorAsync(flow, updater, isNew: false)
                    : await _displayManager.BuildEditorAsync(flow, updater, isNew: false),
            });
        }

        var payment = flow.GetSortedSteps().FirstOrDefault(IsPayment);

        // A promotion code is applied from the payment section, so a post from a page that showed it saves it too.
        if (payment is not null && (paymentShown || (!update && IsReadyToPay(flow))))
        {
            flow.SetCurrentStep(payment.Key);

            page.Sections.Add(new CheckoutOnePageSection
            {
                Step = payment,
                IsPayment = true,
                Shape = update
                    ? await _displayManager.UpdateEditorAsync(flow, updater, isNew: false)
                    : await _displayManager.BuildEditorAsync(flow, updater, isNew: false),
            });

            page.ShowsPayment = true;
        }
        else if (FirstUnsavedStep(flow) is { } unsaved)
        {
            flow.SetCurrentStep(unsaved.Key);
        }

        page.Aside = page.Sections.LastOrDefault()?.Shape;

        return page;
    }

    internal static bool IsReadyToPay(CheckoutFlow flow)
        => FirstUnsavedStep(flow) is null;

    internal static CheckoutFlowStep FirstUnsavedStep(CheckoutFlow flow)
        => flow.GetSortedSteps().FirstOrDefault(step => !IsPayment(step) && step.CollectData && !flow.Session.SavedSteps.ContainsKey(step.Key));

    private static bool IsPayment(CheckoutFlowStep step)
        => string.Equals(step.Key, CheckoutConstants.PaymentStepKey, StringComparison.Ordinal);

    // A checkout that is no longer live is either finished, which has its own page, or over without being paid. The
    // second still gets a page rather than a 404: the customer followed a link to it, and the page is where they find
    // their way back to wherever they started.
    private async Task<IActionResult> ShowEndedAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return NotFound();
        }

        if (await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Completed) is not null)
        {
            return RedirectToRoute(CheckoutConstants.RouteNames.Confirmation, new { sessionId });
        }

        // The store only returns a session that belongs to this caller, whatever its state, so this page never tells
        // one visitor anything about another's checkout.
        var session = await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Canceled)
            ?? await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Expired)
            ?? await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Failed);

        if (session is null)
        {
            return NotFound();
        }

        var returnInfo = CheckoutReturnInfo.GetFrom(session);

        // The page is the CheckoutEnded shape, so a theme can override it.
        return View("Ended", await _shapeFactory.CreateAsync<CheckoutEndedViewModel>("CheckoutEnded", model =>
        {
            model.SessionId = session.SessionId;
            model.Status = session.Status;
            model.ReturnUrl = returnInfo?.ReturnUrl;
            model.ReturnText = returnInfo?.ReturnText;
        }));
    }

    // A checkout is still the customer's to look at while it is waiting on its provider. Serving only
    // "pending" sessions would send somebody whose payment is still processing to a page that does not
    // exist, right after they were told to wait.
    private async Task<CheckoutSession> GetLiveSessionAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        return await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.Pending)
            ?? await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.AwaitingProvider)
            ?? await _sessionStore.GetAsync(sessionId, CheckoutSessionStatus.PaymentPending);
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
                // A step the buyer missed says nothing; a payment gate that refused, such as a promotion code that
                // ran out, says why.
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    await _notifier.ErrorAsync(H["{0}", result.ErrorMessage]);
                }

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
