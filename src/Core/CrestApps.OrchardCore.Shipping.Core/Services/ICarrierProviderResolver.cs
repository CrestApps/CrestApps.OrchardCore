namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Finds the carrier provider the store chose as its default, on Settings › Commerce › Shipping.
/// </summary>
public interface ICarrierProviderResolver
{
    /// <summary>
    /// Gets the default carrier provider, or <see langword="null"/> when none is chosen or it's no longer enabled.
    /// </summary>
    Task<ICarrierProvider> GetDefaultAsync();

    /// <summary>
    /// Gets the carrier providers the store enabled.
    /// </summary>
    Task<IReadOnlyList<ICarrierProvider>> GetEnabledAsync();
}
