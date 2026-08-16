namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Provides the well-known identifiers used by the Addresses module, including the feature id, content
/// type technical names, and content part technical names.
/// </summary>
public static class AddressConstants
{
    /// <summary>
    /// Provides the feature identifiers exposed by the Addresses module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Addresses feature.
        /// </summary>
        public const string ModuleId = "CrestApps.OrchardCore.Addresses";
    }

    /// <summary>
    /// The technical name of the country content type.
    /// </summary>
    public const string Country = "Country";

    /// <summary>
    /// The technical name of the region (state or province) content type.
    /// </summary>
    public const string Region = "Region";

    /// <summary>
    /// The technical name of the city content type.
    /// </summary>
    public const string City = "City";

    /// <summary>
    /// The technical name of the country information part attached to the country content type.
    /// </summary>
    public const string CountryPart = "CountryPart";

    /// <summary>
    /// The technical name of the region information part attached to the region content type.
    /// </summary>
    public const string RegionPart = "RegionPart";

    /// <summary>
    /// The technical name of the city information part attached to the city content type.
    /// </summary>
    public const string CityPart = "CityPart";

    /// <summary>
    /// The technical name of the reusable, attachable address capture part.
    /// </summary>
    public const string AddressPart = "AddressPart";
}
