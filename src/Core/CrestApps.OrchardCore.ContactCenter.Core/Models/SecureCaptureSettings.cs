namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Tenant-scoped site settings that govern agent-assisted secure data capture. Stored via Orchard Core site
/// settings so the policy is isolated per shell/tenant and never shared across tenants.
/// </summary>
public sealed class SecureCaptureSettings
{
    /// <summary>
    /// The smallest secure capture window, in seconds, an operator may configure.
    /// </summary>
    public const int MinLinkTimeToLiveSeconds = 30;

    /// <summary>
    /// The largest secure capture window, in seconds, an operator may configure, bounding it to one hour so a
    /// misconfiguration cannot keep a tokenized capture link valid indefinitely.
    /// </summary>
    public const int MaxLinkTimeToLiveSeconds = 3600;

    /// <summary>
    /// The secure capture window, in seconds, applied when none is configured.
    /// </summary>
    public const int DefaultLinkTimeToLiveSeconds = 300;

    /// <summary>
    /// Gets or sets a value indicating whether agents may initiate secure data capture for this tenant. When
    /// disabled, the platform refuses to start a capture regardless of permission or provider capability.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the lifetime, in seconds, of the one-time customer capture link before it expires.
    /// </summary>
    public int LinkTimeToLiveSeconds { get; set; } = DefaultLinkTimeToLiveSeconds;

    /// <summary>
    /// Gets or sets a value indicating whether starting a capture pauses recording for the duration of the
    /// capture, so a provider that also records the data path cannot retain the sensitive segment.
    /// </summary>
    public bool PauseRecordingDuringCapture { get; set; } = true;
}
