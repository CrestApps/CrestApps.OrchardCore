namespace CrestApps.OrchardCore.TimeTracker.Models;

/// <summary>
/// Where a person stands on a clock that keeps their time.
/// </summary>
public sealed class ExternalTimeClockPunchState
{
    /// <summary>
    /// Gets or sets why the person can't punch on this clock from here, or <see langword="null"/> when they can.
    /// </summary>
    public string Refusal { get; set; }

    /// <summary>
    /// Gets or sets the person's state on the clock, such as Out or Working.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets since when, in UTC.
    /// </summary>
    public DateTime? SinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the punches the person can make next, such as In or BreakStart.
    /// </summary>
    public IReadOnlyList<string> NextPunches { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the clock asks for the person's location with a punch.
    /// </summary>
    public bool NeedsLocation { get; set; }

    /// <summary>
    /// Gets or sets the time zone the clock shows the person's times in.
    /// </summary>
    public string TimeZoneId { get; set; }
}

/// <summary>
/// The outcome of a punch on a clock that keeps a person's time.
/// </summary>
public sealed class ExternalTimeClockPunchResult
{
    /// <summary>
    /// Gets or sets why the punch was refused; empty when it was recorded.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether the punch was recorded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the punch recorded, such as In.
    /// </summary>
    public string Punch { get; set; }

    /// <summary>
    /// Gets or sets when it was recorded, in UTC.
    /// </summary>
    public DateTime? Utc { get; set; }

    /// <summary>
    /// Gets or sets the time zone the clock shows the person's times in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets whether the punch was made at the work site, when the clock checks it.
    /// </summary>
    public bool? WithinGeofence { get; set; }

    /// <summary>
    /// Makes a refused result.
    /// </summary>
    /// <param name="error">Why.</param>
    /// <returns>The result.</returns>
    public static ExternalTimeClockPunchResult Fail(string error) => new() { Errors = [error] };
}
