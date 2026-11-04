namespace CrestApps.OrchardCore.Geo.Models;

/// <summary>
/// The way from one point to another.
/// </summary>
public readonly record struct TravelLeg(TimeSpan Duration, double DistanceMeters);

/// <summary>
/// The legs from each origin to each destination.
/// </summary>
public sealed class TravelMatrix
{
    /// <summary>
    /// Gets or sets the provider that answered.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the legs, by origin then destination.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<TravelLeg>> Legs { get; set; } = [];

    /// <summary>
    /// Gets the leg from an origin to a destination.
    /// </summary>
    /// <param name="origin">The origin's index.</param>
    /// <param name="destination">The destination's index.</param>
    /// <returns>The leg.</returns>
    public TravelLeg this[int origin, int destination] => Legs[origin][destination];
}

/// <summary>
/// Options of a travel-time query.
/// </summary>
public sealed class TravelOptions
{
    /// <summary>
    /// Gets or sets the average speed to assume where the provider estimates, in km/h, such as a territory's.
    /// </summary>
    public double? SpeedKmh { get; set; }
}

/// <summary>
/// A route through points, for drawing on a map.
/// </summary>
public sealed class GeoRoute
{
    /// <summary>
    /// Gets or sets the provider that answered.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the line to draw.
    /// </summary>
    public IReadOnlyList<GeoPoint> Line { get; set; } = [];

    /// <summary>
    /// Gets or sets the whole length, in meters.
    /// </summary>
    public double DistanceMeters { get; set; }

    /// <summary>
    /// Gets or sets the whole time.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the turn-by-turn instructions, when the provider gives them.
    /// </summary>
    public IReadOnlyList<string> Instructions { get; set; } = [];
}
