namespace CrestApps.OrchardCore.Geo.AzureMaps;

/// <summary>
/// The Azure Maps settings.
/// </summary>
public sealed class AzureMapsSettings
{
    /// <summary>
    /// The data protection purpose of the key.
    /// </summary>
    public const string ProtectionPurpose = "CrestApps.OrchardCore.Geo.AzureMaps";

    /// <summary>
    /// Gets or sets the subscription key, encrypted.
    /// </summary>
    public string Key { get; set; }
}
