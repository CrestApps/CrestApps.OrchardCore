namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Limits on the address suggestion endpoints, read from the tenant's <c>CrestApps_Addresses:Autocomplete</c>
/// configuration section.
/// </summary>
public sealed class AddressAutocompleteOptions
{
    /// <summary>
    /// The configuration section the options are read from.
    /// </summary>
    public const string SectionName = "CrestApps_Addresses:Autocomplete";

    /// <summary>
    /// Gets or sets how many suggestion and resolve calls the whole site may make to the paid provider in a day. Zero or
    /// less turns suggestions off: the limit fails closed, it is never lifted.
    /// </summary>
    public int DailyRequestLimit { get; set; } = 10_000;
}
