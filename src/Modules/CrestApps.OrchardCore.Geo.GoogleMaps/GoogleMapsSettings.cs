namespace CrestApps.OrchardCore.Geo.GoogleMaps;

/// <summary>
/// The Google Maps settings.
/// </summary>
public sealed class GoogleMapsSettings
{
    /// <summary>
    /// The data protection purpose of the key.
    /// </summary>
    public const string ProtectionPurpose = "CrestApps.OrchardCore.Geo.GoogleMaps";

    /// <summary>
    /// Gets or sets the API key, encrypted.
    /// </summary>
    public string Key { get; set; }
}
