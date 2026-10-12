using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Stripe.Workflows;

/// <summary>
/// Raises Stripe workflow events when the OrchardCore Workflows feature is enabled, and safely does
/// nothing otherwise. This keeps the Stripe integration independent of the Workflows feature.
/// </summary>
public sealed class StripeWorkflowNotifier
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StripeWorkflowNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeWorkflowNotifier"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the optional workflow manager.</param>
    /// <param name="logger">The logger used to record workflow trigger failures.</param>
    public StripeWorkflowNotifier(
        IServiceProvider serviceProvider,
        ILogger<StripeWorkflowNotifier> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Triggers a Stripe workflow event, if the Workflows feature is available.
    /// </summary>
    /// <param name="eventName">The workflow event name to trigger.</param>
    /// <param name="input">The input made available to resumed workflows.</param>
    /// <param name="correlationId">An optional correlation identifier used to resume a matching workflow.</param>
    /// <returns>A task that completes once the event has been dispatched.</returns>
    public async Task TriggerAsync(string eventName, IDictionary<string, object> input, string correlationId = null)
    {
        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager == null)
        {
            return;
        }

        try
        {
            await workflowManager.TriggerEventAsync(eventName, input, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger the '{EventName}' Stripe workflow event.", eventName);
        }
    }
}
