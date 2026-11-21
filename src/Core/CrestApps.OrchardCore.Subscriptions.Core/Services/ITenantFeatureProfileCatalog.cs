namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Lists the feature profiles a sold site may be given. Orchard Core treats a tenant whose profile it can't find as
/// allowed every feature, so a purchase is checked against what exists, never against a name alone.
/// </summary>
public interface ITenantFeatureProfileCatalog
{
    /// <summary>
    /// Returns the names Orchard Core resolves the host's feature profiles by (the profiles document's keys), or
    /// <see langword="null"/> when they can't be read because the Feature Profiles feature is off.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetProfileIdsAsync(CancellationToken cancellationToken = default);
}
