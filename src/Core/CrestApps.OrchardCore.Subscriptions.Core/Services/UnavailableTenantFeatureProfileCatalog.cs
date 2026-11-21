namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The catalog used while the Feature Profiles feature is off: no profile can be confirmed, so no site is sold.
/// </summary>
public sealed class UnavailableTenantFeatureProfileCatalog : ITenantFeatureProfileCatalog
{
    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> GetProfileIdsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<string>>(null);
}
