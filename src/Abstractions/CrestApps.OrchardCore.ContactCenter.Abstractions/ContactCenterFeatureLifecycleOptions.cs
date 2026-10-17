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

    /// <summary>
    /// The default maximum number of seconds a shutting-down node waits for its in-flight routing work to finish.
    /// </summary>
    public const int DefaultShutdownDrainTimeoutSeconds = 5;

    /// <summary>
    /// Gets or sets the maximum number of seconds a shutting-down node waits for its in-flight routing work to
    /// finish. The wait holds up the host's shutdown, so it is kept short; zero stops taking on new routing work
    /// without waiting at all.
    /// </summary>
    public int ShutdownDrainTimeoutSeconds { get; set; } = DefaultShutdownDrainTimeoutSeconds;
}
