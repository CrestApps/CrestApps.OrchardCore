namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// The OSRM settings.
/// </summary>
public sealed class OsrmSettings
{
    /// <summary>
    /// Gets or sets the server's address, such as <c>http://localhost:5000</c>.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the routing profile the server was built with.
    /// </summary>
    public string Profile { get; set; } = "driving";
}
