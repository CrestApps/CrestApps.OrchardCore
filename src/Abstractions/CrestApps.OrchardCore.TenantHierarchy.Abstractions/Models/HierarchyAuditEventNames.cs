namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Contains the names of the events the parent tenant records in its hierarchy activity log.
/// </summary>
public static class HierarchyAuditEventNames
{
    /// <summary>
    /// A child tenant was created.
    /// </summary>
    public const string Created = "Created";

    /// <summary>
    /// A child tenant finished its setup.
    /// </summary>
    public const string SetupSucceeded = "SetupSucceeded";

    /// <summary>
    /// The setup of a child tenant failed.
    /// </summary>
    public const string SetupFailed = "SetupFailed";

    /// <summary>
    /// A child tenant was edited.
    /// </summary>
    public const string Edited = "Edited";

    /// <summary>
    /// A child tenant was suspended.
    /// </summary>
    public const string Suspended = "Suspended";

    /// <summary>
    /// A child tenant was resumed.
    /// </summary>
    public const string Resumed = "Resumed";

    /// <summary>
    /// A child tenant was reloaded.
    /// </summary>
    public const string Reloaded = "Reloaded";

    /// <summary>
    /// A child tenant was scheduled for removal.
    /// </summary>
    public const string RemovalScheduled = "RemovalScheduled";

    /// <summary>
    /// A child tenant that was scheduled for removal was restored.
    /// </summary>
    public const string Restored = "Restored";

    /// <summary>
    /// A child tenant was removed.
    /// </summary>
    public const string Removed = "Removed";

    /// <summary>
    /// The removal of a child tenant failed.
    /// </summary>
    public const string RemovalFailed = "RemovalFailed";

    /// <summary>
    /// A feature was enabled in a child tenant.
    /// </summary>
    public const string FeatureEnabled = "FeatureEnabled";

    /// <summary>
    /// A feature was disabled in a child tenant.
    /// </summary>
    public const string FeatureDisabled = "FeatureDisabled";

    /// <summary>
    /// An access grant was added.
    /// </summary>
    public const string GrantAdded = "GrantAdded";

    /// <summary>
    /// An access grant was removed.
    /// </summary>
    public const string GrantRemoved = "GrantRemoved";

    /// <summary>
    /// A parent user entered a child tenant.
    /// </summary>
    public const string Entered = "Entered";

    /// <summary>
    /// A request to enter a child tenant was refused.
    /// </summary>
    public const string EntryRefused = "EntryRefused";

    /// <summary>
    /// A delegated access session ended.
    /// </summary>
    public const string SessionEnded = "SessionEnded";

    /// <summary>
    /// A registry entry that the platform changed was dismissed.
    /// </summary>
    public const string Dismissed = "Dismissed";
}
