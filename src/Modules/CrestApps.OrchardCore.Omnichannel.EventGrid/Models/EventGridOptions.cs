namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Models;

/// <summary>
/// Represents the event grid options.
/// </summary>
public sealed class EventGridOptions
{
    /// <summary>
    /// Gets or sets the event grid sas key.
    /// </summary>
    public string EventGridSasKey { get; set; }

    /// <summary>
    /// Gets or sets the a a d issuer.
    /// </summary>
    public string AADIssuer { get; set; }

    /// <summary>
    /// Gets or sets the a a d audience.
    /// </summary>
    public string AADAudience { get; set; }

    /// <summary>
    /// Gets or sets the a a d metadata address.
    /// </summary>
    public string AADMetadataAddress { get; set; }
}
