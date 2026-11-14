namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The point a site's time clock geofence is measured from. The address itself is the team's own
/// (<c>Team.Address</c>); this point is geocoded from it, or set by hand when the entrance is elsewhere.
/// </summary>
public sealed class WorkforceLocation
{
    /// <summary>
    /// Gets or sets the latitude, or <see langword="null"/> when unknown.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude, or <see langword="null"/> when unknown.
    /// </summary>
    public double? Longitude { get; set; }
}
