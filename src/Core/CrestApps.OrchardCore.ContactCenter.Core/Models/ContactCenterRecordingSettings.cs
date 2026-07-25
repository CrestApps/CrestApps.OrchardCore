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
    /// Gets or sets a value indicating whether recording is permitted for this tenant. When disabled the
    /// governance policy fails closed and no interaction may start recording regardless of provider capability.
    /// </summary>
    public bool RecordingEnabled { get; set; } = true;

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
}
