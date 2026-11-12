using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Reads the feature profiles from Orchard Core's Feature Profiles feature (Tenants › Feature Profiles).
/// </summary>
public sealed class FeatureProfilesTenantFeatureProfileCatalog : ITenantFeatureProfileCatalog
{
    private readonly IFeatureProfilesService _featureProfilesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureProfilesTenantFeatureProfileCatalog"/> class.
    /// </summary>
    /// <param name="featureProfilesService">Orchard Core's feature profiles.</param>
    public FeatureProfilesTenantFeatureProfileCatalog(IFeatureProfilesService featureProfilesService)
    {
        _featureProfilesService = featureProfilesService;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetProfileIdsAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _featureProfilesService.GetFeatureProfilesAsync();

        // The plan stores a profile's id (the onboarding editor offers ids); the document is keyed by it too.
        return profiles.Keys
            .Concat(profiles.Values.Select(profile => profile?.Id))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
