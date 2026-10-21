namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Provides the well-known identifiers used by the Addresses module: its features, and the Country content type, part
/// and code field.
/// </summary>
public static class AddressConstants
{
    /// <summary>
    /// Provides the feature identifiers exposed by the Addresses module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Addresses feature: each country's address format and the checks and printing built
        /// on it. It needs no content features.
        /// </summary>
        public const string ModuleId = "CrestApps.OrchardCore.Addresses";

        /// <summary>
        /// The identifier of the Address Countries feature, which keeps the countries a store serves as Country
        /// content items that administrators can rename, add or remove.
        /// </summary>
        public const string Countries = "CrestApps.OrchardCore.Addresses.Countries";

        /// <summary>
        /// Suggests addresses from Google Places as a street is typed.
        /// </summary>
        public const string GooglePlaces = "CrestApps.OrchardCore.Addresses.GooglePlaces";
    }

    /// <summary>
    /// The rate-limit group of the address suggestion endpoints. With Orchard Core's Rate Limits feature on, each
    /// visitor may make a set number of suggestion calls a minute, and a Rate Limits policy can target this group.
    /// </summary>
    public const string AutocompleteRateLimitGroup = "address-autocomplete";

    /// <summary>
    /// The technical name of the country content type.
    /// </summary>
    public const string Country = "Country";

    /// <summary>
    /// The technical name of the country information part attached to the country content type.
    /// </summary>
    public const string CountryPart = "CountryPart";

    /// <summary>
    /// The technical name of the country part's ISO code field.
    /// </summary>
    public const string CodeField = "Code";
}
