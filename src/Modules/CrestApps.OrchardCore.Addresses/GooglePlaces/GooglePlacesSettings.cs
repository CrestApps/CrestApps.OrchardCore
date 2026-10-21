namespace CrestApps.OrchardCore.Addresses.GooglePlaces;

/// <summary>
/// The Google Places key address suggestions are asked for with, kept protected.
/// </summary>
public sealed class GooglePlacesSettings
{
    public const string ProtectionPurpose = "CrestApps.OrchardCore.Addresses.GooglePlaces.ApiKey";

    /// <summary>
    /// Gets or sets the protected API key.
    /// </summary>
    public string ApiKey { get; set; }
}
