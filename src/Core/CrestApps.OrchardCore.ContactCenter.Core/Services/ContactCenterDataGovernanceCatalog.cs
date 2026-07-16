using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the authoritative, code-level classification of every persisted Contact Center data category. It is
/// the single source of truth that backs the data-governance documentation, so retention, access-audit, and
/// erasure decisions reference one registry rather than drifting prose.
/// </summary>
public static class ContactCenterDataGovernanceCatalog
{
    private static readonly IReadOnlyList<ContactCenterDataCategory> _categories =
    [
        new ContactCenterDataCategory
        {
            Key = "interaction-event",
            DisplayName = "Interaction event log",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "InteractionEventRetentionDays, floored by ProjectionReplayHorizonDays and LegalHoldMinimumDays.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "The durable, append-only source of truth for interaction history and the projection-rebuild source. It may reference customer and agent identifiers, so it is personal and bounded by the retention window and its replay/legal-hold floors.",
        },
        new ContactCenterDataCategory
        {
            Key = "interaction",
            DisplayName = "Interaction",
            Sensitivity = ContactCenterDataSensitivity.SensitivePersonal,
            ContainsRecordingReference = true,
            RetentionBasis = "Retained for the life of the interaction record; erasure clears the customer address and delegates recording erasure to the external store.",
            ErasureStrategy = ContactCenterErasureStrategy.Anonymize,
            Description = "The communication history for a single work attempt. It holds the customer address and a recording reference and recording state, so it is sensitive personal data; erasure anonymizes the personal fields while retaining the record for metrics.",
        },
        new ContactCenterDataCategory
        {
            Key = "call-session",
            DisplayName = "Call session",
            Sensitivity = ContactCenterDataSensitivity.SensitivePersonal,
            ContainsRecordingReference = true,
            RetentionBasis = "Retained for the life of the call-session record; erasure clears the from/to addresses and delegates recording erasure to the external store.",
            ErasureStrategy = ContactCenterErasureStrategy.Anonymize,
            Description = "The voice-channel state for a call. It holds the caller and callee addresses and a recording reference and recording state, so it is sensitive personal data.",
        },
        new ContactCenterDataCategory
        {
            Key = "callback-request",
            DisplayName = "Callback request",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "Retained until promoted to an outbound activity or expired; free-text notes may carry additional personal data.",
            ErasureStrategy = ContactCenterErasureStrategy.Anonymize,
            Description = "A queued request to call a customer back. It holds the callback address and free-form notes, so it is personal data.",
        },
        new ContactCenterDataCategory
        {
            Key = "agent-session",
            DisplayName = "Agent session",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "Retained for staffing and adherence reporting; bounded by the agent-session reporting window.",
            ErasureStrategy = ContactCenterErasureStrategy.Anonymize,
            Description = "The presence and state history for an agent. It identifies an individual agent, so it is personal data used for adherence and staffing metrics.",
        },
        new ContactCenterDataCategory
        {
            Key = "agent-profile",
            DisplayName = "Agent profile",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "Managed with the agent's account lifecycle; removed or anonymized when the agent is deprovisioned.",
            ErasureStrategy = ContactCenterErasureStrategy.Anonymize,
            Description = "The routing configuration bound to an agent identity (skills, queues, entitlements). It identifies an individual agent, so it is personal configuration data.",
        },
        new ContactCenterDataCategory
        {
            Key = "outbox-message",
            DisplayName = "Event outbox message",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "Short-lived; deleted on successful dispatch and dead-lettered messages are purged after inspection.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "A durable in-flight domain event awaiting dispatch. Its payload can embed personal data, so it is treated as personal and is bounded by rapid dispatch and dead-letter cleanup.",
        },
        new ContactCenterDataCategory
        {
            Key = "provider-inbox-message",
            DisplayName = "Provider webhook inbox message",
            Sensitivity = ContactCenterDataSensitivity.Personal,
            ContainsRecordingReference = false,
            RetentionBasis = "Short-lived; deleted after successful processing and dead-lettered messages are purged after inspection.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "A durable inbound provider event awaiting processing. Its payload can embed personal data such as caller identifiers, so it is treated as personal and is bounded by rapid processing.",
        },
        new ContactCenterDataCategory
        {
            Key = "provider-command",
            DisplayName = "Provider command",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Short-lived; deleted after the command completes or exhausts retries.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "A leased, fenced outbound instruction to a telephony provider keyed by opaque call and command identifiers, holding no personal data of its own.",
        },
        new ContactCenterDataCategory
        {
            Key = "queue-item",
            DisplayName = "Queue item",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Transient; removed when the work item leaves the queue.",
            ErasureStrategy = ContactCenterErasureStrategy.CascadeWithInteraction,
            Description = "A waiting work item that references an activity and interaction by opaque identifier. It holds no personal data itself and is erased with its interaction.",
        },
        new ContactCenterDataCategory
        {
            Key = "activity-reservation",
            DisplayName = "Activity reservation",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Transient; removed when the reservation is accepted, declined, or expires.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "A short-lived offer of a work item to an agent, keyed by opaque activity and agent claim identifiers, holding no personal data.",
        },
        new ContactCenterDataCategory
        {
            Key = "event-metric",
            DisplayName = "Event metric",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Durable aggregate snapshot retained independently of the event log so it survives event purge.",
            ErasureStrategy = ContactCenterErasureStrategy.NotApplicable,
            Description = "Per-day, per-event-type aggregate counts. It contains no personal data and is retained as the durable metrics snapshot that the projection rebuild reconciles.",
        },
        new ContactCenterDataCategory
        {
            Key = "projection-checkpoint",
            DisplayName = "Projection checkpoint",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Operational; one row per projection, updated in place.",
            ErasureStrategy = ContactCenterErasureStrategy.NotApplicable,
            Description = "The replay cursor, logic version, and last-rebuild time for a projection. It contains no personal data.",
        },
        new ContactCenterDataCategory
        {
            Key = "processed-event",
            DisplayName = "Processed-event ledger",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Operational de-duplication ledger; bounded by the idempotency window.",
            ErasureStrategy = ContactCenterErasureStrategy.RetentionExpiry,
            Description = "A per-handler record of processed event identifiers used for idempotency. It holds only opaque handler and event identifiers.",
        },
        new ContactCenterDataCategory
        {
            Key = "configuration",
            DisplayName = "Routing and dialing configuration",
            Sensitivity = ContactCenterDataSensitivity.NonPersonal,
            ContainsRecordingReference = false,
            RetentionBasis = "Administrator-managed; retained until changed or removed by an administrator.",
            ErasureStrategy = ContactCenterErasureStrategy.NotApplicable,
            Description = "Tenant configuration such as queues, queue groups, skills, entry points, dialer profiles, agent state reason codes, business-hours calendars, and queue memberships. It holds no personal data of end customers.",
        },
    ];

    /// <summary>
    /// Gets the classified Contact Center data categories.
    /// </summary>
    public static IReadOnlyList<ContactCenterDataCategory> Categories => _categories;

    /// <summary>
    /// Attempts to find the data category with the specified key.
    /// </summary>
    /// <param name="key">The stable key of the data category to find.</param>
    /// <param name="category">When this method returns, contains the matching category when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching category exists; otherwise <see langword="false"/>.</returns>
    public static bool TryGet(string key, out ContactCenterDataCategory category)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        foreach (var candidate in _categories)
        {
            if (string.Equals(candidate.Key, key, StringComparison.Ordinal))
            {
                category = candidate;

                return true;
            }
        }

        category = null;

        return false;
    }
}
