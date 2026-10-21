using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Where the store sells from. Taxes sourced at the origin, such as some states' sales tax, use it when an item
/// doesn't say where it ships from.
/// </summary>
public sealed class TaxOriginSettings
{
    /// <summary>
    /// Gets or sets the store's address, or <see langword="null"/> when it isn't set.
    /// </summary>
    public Address Origin { get; set; }
}
