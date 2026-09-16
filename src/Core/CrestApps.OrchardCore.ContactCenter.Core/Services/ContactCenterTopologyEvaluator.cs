using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides whether a deployment satisfies the topology its operator declared.
/// </summary>
/// <remarks>
/// Pure by design. Gathering the observations needs the shell, the feature manager, and the container; deciding
/// what they mean does not. Keeping the decision separate is what lets every branch be tested directly instead
/// of through a booted tenant, which is the difference between a rule that is verified and one that is merely
/// present.
/// </remarks>
public static class ContactCenterTopologyEvaluator
{
    /// <summary>
    /// The Orchard database provider value required by every production topology.
    /// </summary>
    /// <remarks>
    /// PostgreSQL 16 is the only database the support matrix declares production-capable. This is a literal
    /// rather than a reference to Orchard's constant so the pure decision layer stays free of a data-layer
    /// dependency; a contract test pins it to the support matrix.
    /// </remarks>
    public const string RequiredProductionDatabaseProvider = "Postgres";

    /// <summary>
    /// The feature that supplies the Redis connection every other Redis-backed feature depends on.
    /// </summary>
    public const string RedisFeatureId = "OrchardCore.Redis";

    /// <summary>
    /// The feature that replaces the process-local lock with a Redis-backed distributed lock.
    /// </summary>
    public const string RedisLockFeatureId = "OrchardCore.Redis.Lock";

    /// <summary>
    /// The feature that replaces the in-memory SignalR backplane with a Redis-backed one.
    /// </summary>
    public const string SignalRRedisBackplaneFeatureId = "CrestApps.OrchardCore.SignalR.Redis";

    /// <summary>
    /// Evaluates a deployment against the topology it declared.
    /// </summary>
    /// <param name="observations">What the running deployment was observed to be.</param>
    /// <returns>The verdict, listing every unmet requirement.</returns>
    public static ContactCenterTopologyValidationResult Evaluate(ContactCenterTopologyObservations observations)
    {
        ArgumentNullException.ThrowIfNull(observations);

        var declaredProfileId = observations.DeclaredProfileId?.Trim();

        if (string.IsNullOrEmpty(declaredProfileId))
        {
            // An undeclared topology is normal for development, tests, and demos, and is the default. It is not
            // acceptable in a production host, where it would let a deployment escape every requirement below
            // simply by setting nothing.
            return observations.IsProductionHostEnvironment
                ? new ContactCenterTopologyValidationResult
                {
                    DeclaredProfileId = null,
                    IsProductionTopology = false,
                    Failures =
                    [
                        "The host is running in a production environment but no Contact Center topology profile is declared. " +
                        $"Set 'CrestApps:ContactCenter:Topology:ProfileId' to '{ContactCenterTopologyProfiles.SingleNodeDistributedId}'.",
                    ],
                }
                : new ContactCenterTopologyValidationResult
                {
                    DeclaredProfileId = null,
                    IsProductionTopology = false,
                };
        }

        var profile = ContactCenterTopologyProfiles.Find(declaredProfileId);

        if (profile is null)
        {
            // Falling back to the development profile here would turn a typo into a silent downgrade, which is
            // the exact failure this validator exists to prevent.
            return new ContactCenterTopologyValidationResult
            {
                DeclaredProfileId = declaredProfileId,
                IsProductionTopology = false,
                Failures =
                [
                    $"The declared Contact Center topology profile '{declaredProfileId}' is not recognized. " +
                    $"Recognized profiles are: {string.Join(", ", ContactCenterTopologyProfiles.All.Select(candidate => candidate.Id).Order(StringComparer.Ordinal))}.",
                ],
            };
        }

        if (!profile.IsProduction)
        {
            // A non-production topology imposes no infrastructure requirements; it is a statement that the
            // deployment is not claiming support, which is always internally consistent.
            return new ContactCenterTopologyValidationResult
            {
                DeclaredProfileId = profile.Id,
                IsProductionTopology = false,
            };
        }

        var failures = new List<string>();

        if (profile.RequiresSharedRelationalDatabase
            && !string.Equals(observations.DatabaseProvider, RequiredProductionDatabaseProvider, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"Topology '{profile.Id}' requires the '{RequiredProductionDatabaseProvider}' database provider, " +
                $"but this tenant is configured with '{observations.DatabaseProvider ?? "none"}'.");
        }

        if (profile.RequiresRedisDistributedLock || profile.RequiresRedisBackplane)
        {
            // Both Redis-backed features resolve their connection through this one, so a missing base feature is
            // reported once rather than as two derived failures that hide the actual cause.
            if (!observations.RedisFeatureEnabled)
            {
                failures.Add($"Topology '{profile.Id}' requires the '{RedisFeatureId}' feature, which is not enabled.");
            }
        }

        if (profile.RequiresRedisDistributedLock)
        {
            if (!observations.RedisLockFeatureEnabled)
            {
                failures.Add($"Topology '{profile.Id}' requires the '{RedisLockFeatureId}' feature, which is not enabled.");
            }

            if (observations.DistributedLockIsProcessLocal)
            {
                failures.Add(
                    $"Topology '{profile.Id}' requires a distributed lock, but the resolved lock is process-local. " +
                    "A process-local lock cannot serialize two overlapping processes during a rolling restart or a shell reload.");
            }
        }

        if (profile.RequiresRedisBackplane && !observations.SignalRRedisBackplaneFeatureEnabled)
        {
            failures.Add($"Topology '{profile.Id}' requires the '{SignalRRedisBackplaneFeatureId}' feature, which is not enabled.");
        }

        return new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = profile.Id,
            IsProductionTopology = true,
            Failures = failures,
        };
    }
}
