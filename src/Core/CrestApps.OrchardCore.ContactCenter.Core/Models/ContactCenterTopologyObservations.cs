namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What the running deployment actually looks like, gathered once so the topology decision itself stays pure.
/// </summary>
public sealed class ContactCenterTopologyObservations
{
    /// <summary>
    /// Gets the topology profile identifier the operator declared, if any.
    /// </summary>
    public string DeclaredProfileId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the host is running in a production environment.
    /// </summary>
    /// <remarks>
    /// Used only to reject an undeclared topology. A deployment that declares nothing cannot be checked against
    /// anything, so tolerating that outside production and rejecting it inside production is what stops the
    /// validator from being trivially bypassed by omitting configuration.
    /// </remarks>
    public bool IsProductionHostEnvironment { get; init; }

    /// <summary>
    /// Gets the configured Orchard database provider for this tenant.
    /// </summary>
    public string DatabaseProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>OrchardCore.Redis</c> feature is enabled.
    /// </summary>
    public bool RedisFeatureEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>OrchardCore.Redis.Lock</c> feature is enabled.
    /// </summary>
    public bool RedisLockFeatureEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the <c>CrestApps.OrchardCore.SignalR.Redis</c> backplane feature is enabled.
    /// </summary>
    public bool SignalRRedisBackplaneFeatureEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the resolved distributed lock is process-local.
    /// </summary>
    /// <remarks>
    /// Observed from the resolved service rather than inferred from the enabled features, because a feature can
    /// be enabled while the container still hands out the local implementation. The lock that is actually
    /// injected is the one that decides whether two overlapping processes can enter the same critical section.
    /// </remarks>
    public bool DistributedLockIsProcessLocal { get; init; }
}
