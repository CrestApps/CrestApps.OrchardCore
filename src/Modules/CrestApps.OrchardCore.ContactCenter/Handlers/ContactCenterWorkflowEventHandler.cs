using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Bridges Contact Center domain events to OrchardCore Workflows by triggering the
/// <see cref="ContactCenterEvent"/> workflow event for every published domain event.
/// </summary>
public sealed class ContactCenterWorkflowEventHandler : IContactCenterEventHandler
{
    private readonly IWorkflowManager _workflowManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowEventHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger the workflow event.</param>
    public ContactCenterWorkflowEventHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var input = new Dictionary<string, object>
        {
            ["EventType"] = interactionEvent.EventType,
            ["InteractionId"] = interactionEvent.InteractionId,
            ["AggregateType"] = interactionEvent.AggregateType,
            ["AggregateId"] = interactionEvent.AggregateId,
            ["ActorId"] = interactionEvent.ActorId,
            ["SourceComponent"] = interactionEvent.SourceComponent,
        };

        return _workflowManager.TriggerEventAsync(
            nameof(ContactCenterEvent),
            input,
            correlationId: interactionEvent.InteractionId ?? interactionEvent.AggregateId);
    }
}
