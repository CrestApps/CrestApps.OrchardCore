using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Triggers workflow events for subscription lifecycle.
/// </summary>
public sealed class WorkflowSubscriptionHandler : SubscriptionHandlerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowSubscriptionHandler> _logger;

    public WorkflowSubscriptionHandler(
        IServiceProvider serviceProvider,
        ILogger<WorkflowSubscriptionHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task ActivatedAsync(SubscriptionFlowActivatedContext context)
    {
        try
        {
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager == null)
            {
                // Workflows feature is not enabled
                return;
            }

            var input = new Dictionary<string, object>
            {
                ["SessionId"] = context.Session.SessionId,
                ["ContentItemId"] = context.SubscriptionContentItem.ContentItemId,
                ["ContentItemVersionId"] = context.SubscriptionContentItem.ContentItemVersionId,
                ["DisplayText"] = context.SubscriptionContentItem.DisplayText,
                ["OwnerId"] = context.Session.OwnerId,
                ["OwnerName"] = context.Session.OwnerName,
            };

            await workflowManager.TriggerEventAsync(
                SubscriptionActivatedEvent.EventName,
                input,
                correlationId: $"Subscription_{context.Session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering SubscriptionActivatedEvent for session {SessionId}", context.Session.SessionId);
        }
    }

    public override async Task InitializedAsync(SubscriptionFlowInitializedContext context)
    {
        try
        {
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager == null)
            {
                return;
            }

            var input = new Dictionary<string, object>
            {
                ["SessionId"] = context.Session.SessionId,
                ["ContentItemId"] = context.SubscriptionContentItem.ContentItemId,
                ["ContentItemVersionId"] = context.SubscriptionContentItem.ContentItemVersionId,
                ["DisplayText"] = context.SubscriptionContentItem.DisplayText,
                ["OwnerId"] = context.Session.OwnerId,
                ["OwnerName"] = context.Session.OwnerName,
            };

            await workflowManager.TriggerEventAsync(
                SubscriptionInitializedEvent.EventName,
                input,
                correlationId: $"Subscription_{context.Session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering SubscriptionInitializedEvent for session {SessionId}", context.Session.SessionId);
        }
    }

    public override async Task CompletedAsync(SubscriptionFlowCompletedContext context)
    {
        try
        {
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager == null)
            {
                return;
            }

            var input = new Dictionary<string, object>
            {
                ["SessionId"] = context.Session.SessionId,
                ["ContentItemId"] = context.SubscriptionContentItem.ContentItemId,
                ["ContentItemVersionId"] = context.SubscriptionContentItem.ContentItemVersionId,
                ["DisplayText"] = context.SubscriptionContentItem.DisplayText,
                ["OwnerId"] = context.Session.OwnerId,
                ["OwnerName"] = context.Session.OwnerName,
                ["Status"] = context.Session.Status.ToString(),
            };

            await workflowManager.TriggerEventAsync(
                SubscriptionCompletedEvent.EventName,
                input,
                correlationId: $"Subscription_{context.Session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering SubscriptionCompletedEvent for session {SessionId}", context.Session.SessionId);
        }
    }

    public override async Task FailedAsync(SubscriptionFlowFailedContext context)
    {
        try
        {
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager == null)
            {
                return;
            }

            var input = new Dictionary<string, object>
            {
                ["SessionId"] = context.Session.SessionId,
                ["ContentItemId"] = context.SubscriptionContentItem?.ContentItemId,
                ["ContentItemVersionId"] = context.SubscriptionContentItem?.ContentItemVersionId,
                ["DisplayText"] = context.SubscriptionContentItem?.DisplayText,
                ["OwnerId"] = context.Session.OwnerId,
                ["OwnerName"] = context.Session.OwnerName,
                ["ErrorMessage"] = context.ErrorMessage ?? string.Empty,
            };

            await workflowManager.TriggerEventAsync(
                SubscriptionFailedEvent.EventName,
                input,
                correlationId: $"Subscription_{context.Session.SessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering SubscriptionFailedEvent for session {SessionId}", context.Session.SessionId);
        }
    }
}
