namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// What countries call the parts of an address, as the country data names them. An editor turns these into localized
/// field labels.
/// </summary>
public static class AddressLabels
{
    // Regions.
    public const string Area = "area";
    public const string County = "county";
    public const string Department = "department";
    public const string District = "district";
    public const string DoSi = "do_si";
    public const string Emirate = "emirate";
    public const string Island = "island";
    public const string Oblast = "oblast";
    public const string Parish = "parish";
    public const string Prefecture = "prefecture";
    public const string Province = "province";
    public const string State = "state";

    // Cities, besides District.
    public const string City = "city";
    public const string PostTown = "post_town";
    public const string Suburb = "suburb";

    // Dependent localities, besides District and Suburb.
    public const string Neighborhood = "neighborhood";
    public const string Townland = "townland";
    public const string VillageTownship = "village_township";

    // Postal codes.
    public const string Eircode = "eircode";
    public const string Pin = "pin";
    public const string Postal = "postal";
    public const string Zip = "zip";
}
