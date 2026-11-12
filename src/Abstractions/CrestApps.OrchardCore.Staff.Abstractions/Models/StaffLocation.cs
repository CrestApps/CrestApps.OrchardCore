namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A place a staff member starts from, with its coordinates once they are found.
/// </summary>
public sealed class StaffLocation
{
    /// <summary>
    /// Gets or sets the address as it is geocoded.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the latitude, once found.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude, once found.
    /// </summary>
    public double? Longitude { get; set; }
}
