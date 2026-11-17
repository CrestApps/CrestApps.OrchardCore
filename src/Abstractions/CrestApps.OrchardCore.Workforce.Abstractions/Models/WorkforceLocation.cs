using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>
    /// Gets or sets the members this version does not know, kept when the location is saved again, such as the
    /// one-line address of an earlier version until it is moved to the team's own address.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }
}
