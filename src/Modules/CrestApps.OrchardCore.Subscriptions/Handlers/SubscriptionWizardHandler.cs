using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Wizard;
using CrestApps.OrchardCore.Wizard.Handlers;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Handlers;

internal sealed class SubscriptionWizardHandler : WizardHandlerBase
{
    private readonly IEnumerable<ISubscriptionHandler> _subscriptionHandlers;
    private readonly SubscriptionWizardFlowFactory _flowFactory;
    private readonly INotifier _notifier;
    private readonly ILogger<SubscriptionWizardHandler> _logger;
    private readonly IHtmlLocalizer _htmlLocalizer;

    public SubscriptionWizardHandler(
        IEnumerable<ISubscriptionHandler> subscriptionHandlers,
        SubscriptionWizardFlowFactory flowFactory,
        INotifier notifier,
        ILogger<SubscriptionWizardHandler> logger,
        IHtmlLocalizer<SubscriptionWizardHandler> htmlLocalizer)
    {
        _subscriptionHandlers = subscriptionHandlers;
        _flowFactory = flowFactory;
        _notifier = notifier;
        _logger = logger;
        _htmlLocalizer = htmlLocalizer;
    }

    public override Task InitializingAsync(WizardFlowInitializingContext context)
        => InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowInitializingContext(flowContext.SubscriptionSession, flowContext.SubscriptionContentItem),
            (handler, flowContext, handlerContext) => handler.InitializingAsync(handlerContext),
            syncSession: true);

    public override Task LoadingAsync(WizardFlowLoadingContext context)
        => InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowLoadingContext(flowContext.Flow),
            (handler, flowContext, handlerContext) => handler.LoadingAsync(handlerContext),
            syncSession: true);

    public override Task InitializedAsync(WizardFlowInitializedContext context)
        => InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowInitializedContext(flowContext.Flow),
            (handler, flowContext, handlerContext) => handler.InitializedAsync(handlerContext),
            syncSession: true);

    public override Task LoadedAsync(WizardFlowLoadedContext context)
        => InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowLoadedContext(flowContext.Flow),
            (handler, flowContext, handlerContext) => handler.LoadedAsync(handlerContext),
            syncSession: true);

    public override async Task CompletingAsync(WizardFlowCompletingContext context)
    {
        var flowContext = await _flowFactory.CreateAsync(context.Flow);

        if (flowContext == null)
        {
            return;
        }

        var completingContext = new SubscriptionFlowCompletingContext(flowContext.Flow);

        foreach (var handler in _subscriptionHandlers)
        {
            await handler.CompletingAsync(completingContext);
        }

        flowContext.SyncToWizardSession();
    }

    public override Task CompletedAsync(WizardFlowCompletedContext context)
        => InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowCompletedContext(flowContext.Flow),
            (handler, flowContext, handlerContext) => handler.CompletedAsync(handlerContext));

    public override async Task FailedAsync(WizardFlowFailedContext context)
    {
        await InvokeAsync(
            context.Flow,
            flowContext => new SubscriptionFlowFailedContext(flowContext.Flow),
            (handler, flowContext, handlerContext) => handler.FailedAsync(handlerContext));

        await _notifier.ErrorAsync(_htmlLocalizer["Unable to process the subscription at this time. If the issue persists, please contact support."]);
    }

    private async Task InvokeAsync<TContext>(
        WizardFlow wizardFlow,
        Func<SubscriptionWizardFlowContext, TContext> contextFactory,
        Func<ISubscriptionHandler, SubscriptionWizardFlowContext, TContext, Task> callback,
        bool syncSession = false)
    {
        ArgumentNullException.ThrowIfNull(wizardFlow);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(callback);

        var flowContext = await _flowFactory.CreateAsync(wizardFlow);

        if (flowContext == null)
        {
            return;
        }

        var handlerContext = contextFactory(flowContext);

        await _subscriptionHandlers.InvokeAsync(
            (handler, _) => callback(handler, flowContext, handlerContext),
            handlerContext,
            _logger);

        if (syncSession)
        {
            flowContext.SyncToWizardSession();
        }
    }
}
