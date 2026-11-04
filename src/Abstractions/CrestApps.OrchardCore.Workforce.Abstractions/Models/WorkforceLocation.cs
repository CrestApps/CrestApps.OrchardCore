namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A place: an address and, when known, its coordinates.
/// </summary>
public sealed class WorkforceLocation
{
    /// <summary>
    /// Gets or sets the address as one line.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the latitude, or <see langword="null"/> when unknown.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude, or <see langword="null"/> when unknown.
    /// </summary>
    public double? Longitude { get; set; }
}
