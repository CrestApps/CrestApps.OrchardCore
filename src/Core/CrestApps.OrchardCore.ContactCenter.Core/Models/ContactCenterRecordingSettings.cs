namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Tenant-scoped site settings that describe the recording governance policy every voice interaction must satisfy.
/// Stored via Orchard Core site settings so the policy is isolated per shell/tenant and never shared across tenants.
/// </summary>
public sealed class ContactCenterRecordingSettings
{
    /// <summary>
    /// The maximum number of retention days that can be configured, bounding the retention window so it can never
    /// overflow the representable date range when a retention deadline is computed.
    /// </summary>
    public const int MaxRetentionDays = 36500;

    /// <summary>
    /// The maximum number of seconds a recording may be configured to remain paused before automatic resume,
    /// bounding the secure-pause window to a single day so a misconfiguration cannot suppress capture indefinitely.
    /// </summary>
    public const int MaxSecurePauseSecondsLimit = 86400;

    /// <summary>
    /// Gets or sets a value indicating whether every voice call starts recording on its own the moment it connects:
    /// a routed or dialed call once it is bridged to an agent, and an automated voice agent's call once it is
    /// answered. When off, recording starts only when a workflow or a supervisor asks for it.
    /// </summary>
    /// <remarks>
    /// Recording is permitted by enabling the Call Recording feature itself; there is no separate tenant switch. This
    /// defaults to <see langword="true"/> because a tenant that enables recording expects its calls to be recorded
    /// without also building a workflow.
    /// </remarks>
    public bool RecordAllCalls { get; set; } = true;

    /// <summary>
    /// Gets or sets the consent model that governs whether a call may be recorded for this tenant.
    /// </summary>
    public RecordingConsentModel ConsentModel { get; set; } = RecordingConsentModel.AllParties;

    /// <summary>
    /// Gets or sets a value indicating whether explicit, recorded consent must be captured on the interaction
    /// before recording may start. When enabled and consent has not been captured, the policy denies recording.
    /// </summary>
    public bool RequireExplicitConsent { get; set; }

    /// <summary>
    /// Gets or sets the number of days a captured recording is retained before it becomes eligible for erasure.
    /// A value of zero means no automatic retention window is applied and the recording is retained indefinitely.
    /// </summary>
    public int RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether captured recordings begin under legal hold. A recording under legal
    /// hold is exempt from retention-driven and subject-request erasure until the hold is released.
    /// </summary>
    public bool LegalHoldByDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an agent may pause and resume recording on their own live
    /// interaction from the agent desktop. When disabled, only automation (workflow tasks) or the provider may
    /// suppress capture, and the agent-facing secure-pause control is never shown.
    /// </summary>
    public bool AllowAgentSecurePause { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of seconds a recording may remain paused for a sensitive-data capture
    /// before the platform automatically resumes it. A value of zero means no automatic resume guard is applied
    /// and a pause persists until it is explicitly resumed.
    /// </summary>
    public int MaxSecurePauseSeconds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an agent must supply a reason when pausing recording, so the
    /// suppression gap in the recording is always accompanied by an auditable justification.
    /// </summary>
    public bool RequirePauseReason { get; set; }
}
