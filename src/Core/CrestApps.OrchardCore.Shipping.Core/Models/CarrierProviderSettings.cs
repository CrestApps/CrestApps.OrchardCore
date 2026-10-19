namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// Which carrier provider the store uses, on the Providers tab of Settings › Commerce › Shipping.
/// </summary>
public sealed class CarrierProviderSettings
{
    /// <summary>
    /// Gets or sets the technical name of the default carrier provider, or <see langword="null"/> for none.
    /// </summary>
    public string DefaultProviderName { get; set; }
}
