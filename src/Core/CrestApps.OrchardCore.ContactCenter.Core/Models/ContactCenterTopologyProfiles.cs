using System.Collections.Frozen;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The deployment topologies this release recognizes.
/// </summary>
/// <remarks>
/// These are the deployment topologies this release recognizes and the single production topology it
/// certifies. Adding a topology here changes what the product accepts as a supported deployment.
/// </remarks>
public static class ContactCenterTopologyProfiles
{
    /// <summary>
    /// The identifier of the single production topology this release earns: exactly one application node
    /// running the full distributed contract.
    /// </summary>
    public const string SingleNodeDistributedId = "single-node-distributed";

    /// <summary>
    /// The identifier of the multi-node topology. It remains the architectural direction but is not
    /// production-certified in this release, because multi-node capacity certification has not been earned.
    /// </summary>
    public const string SingleRegionMultiNodeId = "single-region-multi-node";

    /// <summary>
    /// The identifier of the development topology. It requires no distributed infrastructure and is never
    /// supported for production use.
    /// </summary>
    public const string SingleNodeDevelopmentId = "single-node-development";

    private static readonly FrozenDictionary<string, ContactCenterTopologyProfile> _profiles =
        new Dictionary<string, ContactCenterTopologyProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [SingleNodeDistributedId] = new ContactCenterTopologyProfile
            {
                Id = SingleNodeDistributedId,
                IsProduction = true,
                MinimumApplicationNodes = 1,
                MaximumApplicationNodes = 1,
                RequiresRedisBackplane = true,
                RequiresRedisDistributedLock = true,
                RequiresSharedRelationalDatabase = true,
            },
            [SingleRegionMultiNodeId] = new ContactCenterTopologyProfile
            {
                Id = SingleRegionMultiNodeId,
                IsProduction = false,
                MinimumApplicationNodes = 2,
                MaximumApplicationNodes = 4,
                RequiresRedisBackplane = true,
                RequiresRedisDistributedLock = true,
                RequiresSharedRelationalDatabase = true,
            },
            [SingleNodeDevelopmentId] = new ContactCenterTopologyProfile
            {
                Id = SingleNodeDevelopmentId,
                IsProduction = false,
                MinimumApplicationNodes = 1,
                MaximumApplicationNodes = 1,
                RequiresRedisBackplane = false,
                RequiresRedisDistributedLock = false,
                RequiresSharedRelationalDatabase = false,
            },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets every recognized topology profile.
    /// </summary>
    public static IReadOnlyCollection<ContactCenterTopologyProfile> All => _profiles.Values;

    /// <summary>
    /// Finds the profile an operator declared.
    /// </summary>
    /// <param name="id">The declared profile identifier.</param>
    /// <returns>The matching profile, or <see langword="null"/> when the identifier is not recognized.</returns>
    /// <remarks>
    /// An unrecognized identifier deliberately returns <see langword="null"/> rather than falling back to the
    /// development profile. A typo in a production deployment must surface as a validation failure, not as a
    /// silent downgrade to the topology with no requirements.
    /// </remarks>
    public static ContactCenterTopologyProfile Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _profiles.TryGetValue(id.Trim(), out var profile)
            ? profile
            : null;
    }
}
