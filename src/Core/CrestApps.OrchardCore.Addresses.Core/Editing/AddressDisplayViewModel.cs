using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Core.Editing;

/// <summary>
/// An address to print, as the <c>AddressDisplay</c> shape shows it.
/// </summary>
public class AddressDisplayViewModel
{
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets whether the last line names the country.
    /// </summary>
    public bool IncludeCountry { get; set; } = true;
}
