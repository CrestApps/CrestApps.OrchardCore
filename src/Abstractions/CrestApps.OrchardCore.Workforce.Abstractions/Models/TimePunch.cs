using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One punch on the time clock. Punches are never changed: a correction is a new punch that replaces the old one, and
/// a removal is a new punch marked as removing it.
/// </summary>
public sealed class TimePunch : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team the worker punched for.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets what the punch records.
    /// </summary>
    public TimePunchKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the punch.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets where the punch came from.
    /// </summary>
    public TimePunchSource Source { get; set; }

    /// <summary>
    /// Gets or sets the device or kiosk that sent it.
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the latitude the punch was made at, when shared.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude the punch was made at, when shared.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets whether the punch was inside the work site; <see langword="null"/> when not checked.
    /// </summary>
    public bool? WithinGeofence { get; set; }

    /// <summary>
    /// Gets or sets the activity worked from this punch on.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the job, work order or visit worked from this punch on.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// Gets or sets a key that makes a punch sent twice count once, such as an offline punch's id or an event id.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the punch this one corrects or removes.
    /// </summary>
    public string ReplacesPunchId { get; set; }

    /// <summary>
    /// Gets or sets whether this punch only removes the one it replaces.
    /// </summary>
    public bool Removes { get; set; }

    /// <summary>
    /// Gets or sets why a manager added, changed or removed a punch.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets who recorded it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
