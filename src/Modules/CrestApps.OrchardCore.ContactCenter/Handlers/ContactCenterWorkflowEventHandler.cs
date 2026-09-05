using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Bridges Contact Center domain events to OrchardCore Workflows by triggering the
/// <see cref="ContactCenterEvent"/> workflow event for every published domain event. Because outbox
/// delivery is at-least-once, the bridge dedupes on the durable event id so a replayed event never starts
/// a duplicate workflow.
/// </summary>
public sealed class ContactCenterWorkflowEventHandler : IContactCenterEventHandler
{
    private readonly IWorkflowManager _workflowManager;
    private readonly IContactCenterEventDeduplicationService _deduplicationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowEventHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger the workflow event.</param>
    /// <param name="deduplicationService">The durable per-handler event deduplication service.</param>
    public ContactCenterWorkflowEventHandler(
        IWorkflowManager workflowManager,
        IContactCenterEventDeduplicationService deduplicationService)
    {
        _workflowManager = workflowManager;
        _deduplicationService = deduplicationService;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/WorkflowBridge/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.DeduplicatedByEventId;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (string.IsNullOrEmpty(interactionEvent.ItemId))
        {
            return;
        }

        // Reserve the event before triggering so a replayed delivery cannot start a second workflow; the
        // reservation and the triggered workflow state commit together in the outbox session.
        if (!await _deduplicationService.TryBeginAsync(HandlerId, interactionEvent.ItemId, cancellationToken))
        {
            return;
        }

        var input = new Dictionary<string, object>
        {
            ["EventType"] = interactionEvent.EventType,
            ["InteractionId"] = interactionEvent.InteractionId,
            ["AggregateType"] = interactionEvent.AggregateType,
            ["AggregateId"] = interactionEvent.AggregateId,
            ["ActorId"] = interactionEvent.ActorId,
            ["SourceComponent"] = interactionEvent.SourceComponent,
        };

        await _workflowManager.TriggerEventAsync(
            nameof(ContactCenterEvent),
            input,
            correlationId: interactionEvent.InteractionId ?? interactionEvent.AggregateId);
    }
}
