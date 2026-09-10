namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Configures bounded Contact Center feature work draining during Orchard shell replacement.
/// </summary>
public sealed class ContactCenterFeatureLifecycleOptions
{
    /// <summary>
    /// The default maximum number of seconds feature disable waits for admitted work to finish.
    /// </summary>
    public const int DefaultDrainTimeoutSeconds = 30;

    /// <summary>
    /// Gets or sets the maximum number of seconds feature disable waits for admitted work to finish.
    /// </summary>
    public int DrainTimeoutSeconds { get; set; } = DefaultDrainTimeoutSeconds;
}
