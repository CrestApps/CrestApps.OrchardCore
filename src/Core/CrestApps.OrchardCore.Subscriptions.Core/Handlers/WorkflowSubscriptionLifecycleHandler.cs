using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Raises a workflow event whenever a subscription changes state, so a site owner can build the reaction
/// they want without writing code.
/// </summary>
/// <remarks>
/// The reactions differ from site to site: welcome the new subscriber, warn the one whose card was
/// declined, ask the one who cancelled why, tell a channel. Hard-coding any of them would be wrong, and a
/// setting per reaction would never end.
///
/// Only a genuine status change raises an event. Firing on every write would send a customer a "your
/// payment failed" message each time a sweep touched their record.
/// </remarks>
public sealed class WorkflowSubscriptionLifecycleHandler : SubscriptionLifecycleHandlerBase
{
    private readonly IWorkflowManager _workflowManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowSubscriptionLifecycleHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to raise the events.</param>
    /// <param name="logger">The logger.</param>
    public WorkflowSubscriptionLifecycleHandler(
        IWorkflowManager workflowManager,
        ILogger<WorkflowSubscriptionLifecycleHandler> logger)
    {
        _workflowManager = workflowManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ChangedAsync(SubscriptionLifecycleContext context)
    {
        if (context is null || !context.StatusChanged)
        {
            return;
        }

        var eventName = ResolveEventName(context);

        if (eventName is null)
        {
            return;
        }

        var subscription = context.Subscription;

        try
        {
            await _workflowManager.TriggerEventAsync(
                eventName,
                new Dictionary<string, object>
                {
                    ["SubscriptionId"] = subscription.ItemId,
                    ["Title"] = subscription.Title,
                    ["OwnerId"] = subscription.OwnerId,
                    ["Status"] = subscription.Status.ToString(),
                    ["PreviousStatus"] = context.PreviousStatus.ToString(),
                    ["Currency"] = subscription.Currency,
                    ["Amount"] = subscription.TotalAmount,
                    ["CurrentPeriodEndUtc"] = subscription.CurrentPeriodEndUtc,
                    ["GraceEndsUtc"] = subscription.GraceEndsUtc,
                },

                // Correlating by the subscription lets a workflow wait for a later stage of the same
                // agreement, for example expiry after a past-due warning.
                correlationId: subscription.ItemId);
        }
        catch (Exception exception)
        {
            // A workflow that fails must not roll back the transition. The agreement's state is the fact.
            _logger.LogError(exception, "Failed to raise the '{EventName}' workflow event for subscription '{SubscriptionId}'.", eventName, subscription.ItemId);
        }
    }

    private static string ResolveEventName(SubscriptionLifecycleContext context)
        => context.Subscription.Status switch
        {
            // A subscription reaching Active from Incomplete is the customer's first successful payment;
            // reaching it from PastDue or Paused is a recovery, which is a renewal, not a new start.
            SubscriptionStatus.Active when context.PreviousStatus == SubscriptionStatus.Incomplete => SubscriptionStartedEvent.EventName,
            SubscriptionStatus.Active => SubscriptionRenewedEvent.EventName,
            SubscriptionStatus.PastDue => SubscriptionPastDueEvent.EventName,
            SubscriptionStatus.Canceled => SubscriptionCanceledEvent.EventName,
            SubscriptionStatus.Expired => SubscriptionExpiredEvent.EventName,
            _ => null,
        };
}
