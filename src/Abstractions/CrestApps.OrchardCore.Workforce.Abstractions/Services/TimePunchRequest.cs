using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A punch to record.
/// </summary>
public sealed class TimePunchRequest
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets what the punch records.
    /// </summary>
    public TimePunchKind Kind { get; set; }

    /// <summary>
    /// Gets or sets where it came from.
    /// </summary>
    public TimePunchSource Source { get; set; } = TimePunchSource.Web;

    /// <summary>
    /// Gets or sets the UTC time it was made, for a punch made offline or by another system; <see langword="null"/>
    /// for now.
    /// </summary>
    public DateTime? Utc { get; set; }

    /// <summary>
    /// Gets or sets the device or kiosk.
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the latitude, when shared.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude, when shared.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the activity from this punch on.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the job from this punch on.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// Gets or sets a key that makes a punch sent twice count once.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets why, for a manager's punch.
    /// </summary>
    public string Reason { get; set; }
}
