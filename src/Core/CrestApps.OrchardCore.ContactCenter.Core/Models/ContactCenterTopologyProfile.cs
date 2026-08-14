namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A deployment topology an operator may declare, and the infrastructure that topology requires.
/// </summary>
/// <remarks>
/// This is the shipped, code-side definition of the deployment topologies the product recognizes. The
/// running application relies on it to decide whether a deployment satisfies its declared topology.
/// </remarks>
public sealed class ContactCenterTopologyProfile
{
    /// <summary>
    /// Gets the identifier an operator declares in configuration to select this topology.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets a value indicating whether this topology is supported for production use.
    /// </summary>
    public required bool IsProduction { get; init; }

    /// <summary>
    /// Gets the smallest number of application nodes this topology is certified for.
    /// </summary>
    public required int MinimumApplicationNodes { get; init; }

    /// <summary>
    /// Gets the largest number of application nodes this topology is certified for.
    /// </summary>
    public required int MaximumApplicationNodes { get; init; }

    /// <summary>
    /// Gets a value indicating whether this topology requires the Redis SignalR backplane.
    /// </summary>
    public required bool RequiresRedisBackplane { get; init; }

    /// <summary>
    /// Gets a value indicating whether this topology requires Redis-backed distributed locking.
    /// </summary>
    public required bool RequiresRedisDistributedLock { get; init; }

    /// <summary>
    /// Gets a value indicating whether this topology requires a shared relational database rather than a
    /// file-backed one.
    /// </summary>
    public required bool RequiresSharedRelationalDatabase { get; init; }
}
