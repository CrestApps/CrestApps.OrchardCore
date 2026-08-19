using CrestApps.OrchardCore.Wizard.Core;
using CrestApps.OrchardCore.Wizard.Services;
using CrestApps.OrchardCore.Wizard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Wizard.Controllers;

/// <summary>
/// Handles the public, reusable wizard experience: starting or resuming a session, saving a step and
/// advancing, displaying a saved step, and showing the confirmation of a completed session.
/// </summary>
public sealed class WizardController : Controller
{
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<WizardFlow> _wizardFlowDisplayManager;
    private readonly IEnumerable<Handlers.IWizardHandler> _wizardHandlers;
    private readonly IWizardSessionStore _wizardSessionStore;
    private readonly IWizardEngine _wizardEngine;
    private readonly IWizardDefinitionProvider _wizardDefinitionProvider;
    private readonly IEnumerable<IWizardAccessPolicy> _accessPolicies;
    private readonly WizardResumeCookieManager _cookieManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;
    private readonly ILogger<WizardController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WizardController"/> class.
    /// </summary>
    /// <param name="updateModelAccessor">The accessor that provides the current model updater.</param>
    /// <param name="wizardFlowDisplayManager">The display manager used to build wizard flow shapes.</param>
    /// <param name="wizardHandlers">The handlers that participate in wizard flow lifecycle events.</param>
    /// <param name="wizardSessionStore">The store used to create, load, and save wizard sessions.</param>
    /// <param name="wizardEngine">The engine used to finalize a wizard under a lock.</param>
    /// <param name="wizardDefinitionProvider">The provider used to resolve registered wizard definitions.</param>
    /// <param name="accessPolicies">The per-instance access policies that can require an authenticated visitor.</param>
    /// <param name="cookieManager">The data-protected resume cookie manager.</param>
    /// <param name="authorizationService">The authorization service used to guard wizard execution.</param>
    /// <param name="clock">The clock used to assign wizard timestamps.</param>
    /// <param name="logger">The logger used to record wizard flow errors.</param>
    public WizardController(
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<WizardFlow> wizardFlowDisplayManager,
        IEnumerable<Handlers.IWizardHandler> wizardHandlers,
        IWizardSessionStore wizardSessionStore,
        IWizardEngine wizardEngine,
        IWizardDefinitionProvider wizardDefinitionProvider,
        IEnumerable<IWizardAccessPolicy> accessPolicies,
        WizardResumeCookieManager cookieManager,
        IAuthorizationService authorizationService,
        IClock clock,
        ILogger<WizardController> logger)
    {
        _updateModelAccessor = updateModelAccessor;
        _wizardFlowDisplayManager = wizardFlowDisplayManager;
        _wizardHandlers = wizardHandlers;
        _wizardSessionStore = wizardSessionStore;
        _wizardEngine = wizardEngine;
        _wizardDefinitionProvider = wizardDefinitionProvider;
        _accessPolicies = accessPolicies;
        _cookieManager = cookieManager;
        _authorizationService = authorizationService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Starts or resumes a pending wizard session for the given wizard type.
    /// </summary>
    /// <param name="wizardType">The wizard type discriminator.</param>
    /// <param name="definitionId">The optional identifier of the definition the wizard is started from.</param>
    /// <returns>The wizard view, or a not found result when the wizard type is not registered.</returns>
    [HttpGet("Wizard/Start/{wizardType}", Name = WizardConstants.RouteNames.Start)]
    public async Task<IActionResult> Start(string wizardType, string definitionId)
    {
        var definition = GetDefinition(wizardType);

        if (definition == null)
        {
            return NotFound();
        }

        if (definition.RequiresAuthenticatedUser && !User.Identity.IsAuthenticated)
        {
            return Challenge();
        }

        if (!User.Identity.IsAuthenticated && await RequiresAuthenticatedUserAsync(wizardType, definitionId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, WizardPermissions.StartWizard))
        {
            return Forbid();
        }

        var wizardKey = GetWizardKey(wizardType, definitionId);

        WizardSession session = null;

        if (_cookieManager.TryGetValue(wizardKey, out var existingSessionId) && !string.IsNullOrEmpty(existingSessionId))
        {
            session = await _wizardSessionStore.GetAsync(existingSessionId, WizardSessionStatus.Pending);
        }

        var isNewSession = session == null;

        session ??= await _wizardSessionStore.NewAsync(wizardType, definitionId);

        var flow = await InitializeFlowAsync(session);

        session.CurrentStep ??= flow.GetFirstStep()?.Key;

        var content = await _wizardFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        await _wizardHandlers.InvokeAsync((handler, ctx) => handler.LoadedAsync(ctx), new WizardFlowLoadedContext(flow), _logger);

        if (isNewSession)
        {
            session.ModifiedUtc = _clock.UtcNow;

            await _wizardSessionStore.SaveAsync(session);
        }

        _cookieManager.Append(wizardKey, session.SessionId);

        return View(new WizardViewModel
        {
            WizardType = wizardType,
            DefinitionId = definitionId,
            SessionId = session.SessionId,
            Step = session.CurrentStep,
            Content = content,
        });
    }

    /// <summary>
    /// Saves posted wizard step data and advances, completes, or redisplays the wizard flow.
    /// </summary>
    /// <param name="model">The posted wizard view model.</param>
    /// <returns>The next step redirect, confirmation redirect, redisplayed wizard view, or a not found result.</returns>
    [HttpPost("Wizard/Start/{wizardType}")]
    [ActionName(nameof(Start))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPOST(WizardViewModel model)
    {
        var definition = GetDefinition(model.WizardType);

        if (definition == null)
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, WizardPermissions.ContinueWizard))
        {
            return Forbid();
        }

        var wizardKey = GetWizardKey(model.WizardType, model.DefinitionId);

        WizardSession session = null;

        if (!string.IsNullOrWhiteSpace(model.SessionId))
        {
            session = await _wizardSessionStore.GetAsync(model.SessionId, WizardSessionStatus.Pending);

            if (session != null &&
                !string.IsNullOrEmpty(model.Step) &&
                !string.Equals(model.Step, session.CurrentStep, StringComparison.OrdinalIgnoreCase) &&
                session.SavedSteps.ContainsKey(model.Step))
            {
                session.CurrentStep = model.Step;
            }
        }

        if (session == null)
        {
            return NotFound();
        }

        var flow = await InitializeFlowAsync(session);

        var content = await _wizardFlowDisplayManager.UpdateEditorAsync(flow, _updateModelAccessor.ModelUpdater, true);

        if (_updateModelAccessor.ModelUpdater.ModelState.IsValid)
        {
            var now = _clock.UtcNow;

            session.ModifiedUtc = now;

            _cookieManager.Append(wizardKey, session.SessionId);

            var upcomingStep = flow.GetNextStep();

            if (upcomingStep != null)
            {
                flow.SetCurrentStep(upcomingStep.Key);

                await _wizardSessionStore.SaveAsync(session);

                return RedirectToAction(nameof(Display), new
                {
                    sessionId = session.SessionId,
                    step = upcomingStep.Key,
                });
            }

            var blockingStep = GetFirstIncompleteStep(flow);

            if (blockingStep != null)
            {
                flow.SetCurrentStep(blockingStep.Key);

                await _wizardSessionStore.SaveAsync(session);

                return RedirectToAction(nameof(Display), new
                {
                    sessionId = session.SessionId,
                    step = blockingStep.Key,
                });
            }

            await _wizardSessionStore.SaveAsync(session);

            var result = await _wizardEngine.CompleteAsync(flow);

            if (result.IsCompleted)
            {
                _cookieManager.Remove(wizardKey);

                return RedirectToAction(nameof(Confirmation), new
                {
                    sessionId = session.SessionId,
                });
            }

            if (result.Status == WizardCompletionStatus.Blocked && !string.IsNullOrEmpty(result.BlockingStepKey))
            {
                flow.SetCurrentStep(result.BlockingStepKey);

                await _wizardSessionStore.SaveAsync(session);

                return RedirectToAction(nameof(Display), new
                {
                    sessionId = session.SessionId,
                    step = result.BlockingStepKey,
                });
            }
        }

        model.Step = flow.GetCurrentStep()?.Key;
        model.Content = content;

        return View(nameof(Start), model);
    }

    /// <summary>
    /// Displays a saved step from a pending wizard session.
    /// </summary>
    /// <param name="sessionId">The wizard session identifier.</param>
    /// <param name="step">The optional saved step key to display.</param>
    /// <returns>The wizard step view, or a not found result when the session cannot be loaded.</returns>
    [HttpGet("Wizard/Step/{sessionId}/{step?}", Name = WizardConstants.RouteNames.Step)]
    public async Task<IActionResult> Display(string sessionId, string step)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WizardPermissions.ContinueWizard))
        {
            return Forbid();
        }

        var session = await _wizardSessionStore.GetAsync(sessionId, WizardSessionStatus.Pending);

        if (session == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(step) && session.SavedSteps.ContainsKey(step))
        {
            session.CurrentStep = step;
        }

        var flow = await InitializeFlowAsync(session);

        var content = await _wizardFlowDisplayManager.BuildEditorAsync(flow, _updateModelAccessor.ModelUpdater, false);

        await _wizardHandlers.InvokeAsync((handler, ctx) => handler.LoadedAsync(ctx), new WizardFlowLoadedContext(flow), _logger);

        return View(nameof(Start), new WizardViewModel
        {
            WizardType = session.WizardType,
            DefinitionId = session.DefinitionId,
            SessionId = session.SessionId,
            Step = session.CurrentStep,
            Content = content,
        });
    }

    /// <summary>
    /// Displays the confirmation page for a completed wizard session.
    /// </summary>
    /// <param name="sessionId">The wizard session identifier.</param>
    /// <returns>The confirmation view, or a not found result when the session cannot be loaded.</returns>
    [HttpGet("Wizard/Confirmation/{sessionId}", Name = WizardConstants.RouteNames.Confirmation)]
    public async Task<IActionResult> Confirmation(string sessionId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WizardPermissions.ContinueWizard))
        {
            return Forbid();
        }

        var session = await _wizardSessionStore.GetAsync(sessionId, WizardSessionStatus.Completed);

        if (session == null)
        {
            return NotFound();
        }

        var flow = new WizardFlow(session);

        var content = await _wizardFlowDisplayManager.BuildDisplayAsync(flow, _updateModelAccessor.ModelUpdater, "Confirmation");

        return View(new WizardViewModel
        {
            WizardType = session.WizardType,
            DefinitionId = session.DefinitionId,
            SessionId = session.SessionId,
            Content = content,
        });
    }

    private async Task<WizardFlow> InitializeFlowAsync(WizardSession session)
    {
        await _wizardHandlers.InvokeAsync((handler, ctx) => handler.InitializingAsync(ctx), new WizardFlowInitializingContext(new WizardFlow(session)), _logger);

        var flow = new WizardFlow(session);

        await _wizardHandlers.InvokeAsync((handler, ctx) => handler.LoadingAsync(ctx), new WizardFlowLoadingContext(flow), _logger);
        await _wizardHandlers.InvokeAsync((handler, ctx) => handler.InitializedAsync(ctx), new WizardFlowInitializedContext(flow), _logger);

        return flow;
    }

    private IWizardDefinition GetDefinition(string wizardType)
    {
        if (string.IsNullOrEmpty(wizardType))
        {
            return null;
        }

        return _wizardDefinitionProvider.GetDefinitions()
            .FirstOrDefault(definition => string.Equals(definition.WizardType, wizardType, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId)
    {
        foreach (var policy in _accessPolicies)
        {
            if (await policy.RequiresAuthenticatedUserAsync(wizardType, definitionId))
            {
                return true;
            }
        }

        return false;
    }

    private static WizardStep GetFirstIncompleteStep(WizardFlow flow)
    {
        foreach (var step in flow.GetSortedSteps())
        {
            if (step.CollectData && !flow.Session.SavedSteps.ContainsKey(step.Key))
            {
                return step;
            }
        }

        return null;
    }

    private static string GetWizardKey(string wizardType, string definitionId)
        => string.IsNullOrEmpty(definitionId) ? wizardType : $"{wizardType}:{definitionId}";
}
