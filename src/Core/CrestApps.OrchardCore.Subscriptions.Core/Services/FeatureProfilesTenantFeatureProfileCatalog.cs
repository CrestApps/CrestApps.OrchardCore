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

        // Orchard Core resolves a tenant's profile by the document's key alone (FeatureProfilesValidationProvider), and a
        // name it can't find allows every feature. A profile's own Id is not always its key: the FeatureProfiles recipe step
        // keys a profile by its property name, whatever Id its body carries. So only the keys are names a plan may use.
        return profiles.Keys
            .Where(key => !string.IsNullOrEmpty(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
