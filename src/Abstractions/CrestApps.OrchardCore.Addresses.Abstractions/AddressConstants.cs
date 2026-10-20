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

    /// <summary>
    /// The technical name of the region (state or province) content type of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string Region = "Region";

    /// <summary>
    /// The technical name of the county content type of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string County = "County";

    /// <summary>
    /// The technical name of the city content type of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string City = "City";

    /// <summary>
    /// The technical name of the district content type of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string District = "District";

    /// <summary>
    /// The technical name of the region part of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string RegionPart = "RegionPart";

    /// <summary>
    /// The technical name of the county part of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string CountyPart = "CountyPart";

    /// <summary>
    /// The technical name of the city part of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string CityPart = "CityPart";

    /// <summary>
    /// The technical name of the district part of earlier 3.0 previews.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string DistrictPart = "DistrictPart";

    /// <summary>
    /// The technical name of the address part of earlier 3.0 previews, whose places are content pickers.
    /// </summary>
    [Obsolete(ObsoleteGeography)]
    public const string AddressPart = "AddressPart";

    /// <summary>
    /// Why the geography of earlier 3.0 previews is obsolete, and what replaces it.
    /// </summary>
    public const string ObsoleteGeography = "AddressPart and the Region, County, City and District types are replaced by the Address value and the Address field. They will be removed in the next major version.";
}
