using System.Collections.Frozen;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The deployment topologies this release recognizes.
/// </summary>
/// <remarks>
/// Kept in lockstep with <c>.github/contact-center/support-matrix.v1.json</c> by a contract test. Adding a
/// topology here without adding it there — or vice versa — fails the build, because a topology the product
/// accepts but the support contract does not publish is an unsupported deployment the product treats as
/// supported.
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
