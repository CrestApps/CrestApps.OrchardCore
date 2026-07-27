using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Maintenance;

/// <summary>
/// Declares every persisted Contact Center document type that the preview maintenance tooling exports and
/// resets, and the governance category that classifies it.
/// </summary>
/// <remarks>
/// This registry is the single source of truth for the export and reset tooling. A completeness gate compares
/// it against the persisted document types discovered from the registered YesSql index providers, so a new
/// persisted type cannot be added without also being exportable and resettable.
/// </remarks>
public static class ContactCenterPreviewDataSetRegistry
{
    private static readonly IReadOnlyList<ContactCenterPreviewDataSetDescriptor> _descriptors =
    [
        new ContactCenterPreviewDataSetDescriptor(typeof(InteractionEvent), "interaction-event", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(Interaction), "interaction", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(CallSession), "call-session", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(CallbackRequest), "callback-request", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(AgentSession), "agent-session", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(QueueItem), "queue-item", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ActivityReservation), "activity-reservation", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterOutboxMessage), "outbox-message", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ProviderWebhookInboxMessage), "provider-inbox-message", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ProviderCommand), "provider-command", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterEventMetric), "event-metric", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterProjectionCheckpoint), "projection-checkpoint", isConfiguration: false),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterProcessedEvent), "processed-event", isConfiguration: false),

        // The agent roster is operator-authored: an agent's skills and queue membership are configuration, so an
        // operational reset preserves the roster and clears only the live sign-in state held by AgentSession.
        new ContactCenterPreviewDataSetDescriptor(typeof(AgentProfile), "agent-profile", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(ActivityQueue), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(ActivityQueueGroup), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterSkill), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(ContactCenterEntryPoint), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(AgentStateReasonCode), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(BusinessHoursCalendar), "configuration", isConfiguration: true),
        new ContactCenterPreviewDataSetDescriptor(typeof(DialerProfile), "configuration", isConfiguration: true),
    ];

    /// <summary>
    /// Gets every declared preview data set descriptor.
    /// </summary>
    public static IReadOnlyList<ContactCenterPreviewDataSetDescriptor> Descriptors => _descriptors;
}
