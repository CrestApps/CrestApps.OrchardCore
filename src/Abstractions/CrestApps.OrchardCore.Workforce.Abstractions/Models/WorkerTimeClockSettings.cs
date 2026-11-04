namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A worker's kiosk PIN, kept in the worker's properties. Their employee number is their badge number.
/// </summary>
public sealed class WorkerTimeClockSettings
{
    /// <summary>
    /// Gets or sets the PIN's salted hash.
    /// </summary>
    public string PinHash { get; set; }

    /// <summary>
    /// Gets or sets the PIN's salt.
    /// </summary>
    public string PinSalt { get; set; }

    /// <summary>
    /// Gets or sets the wrong PINs in a row.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the kiosk accepts the worker's PIN again after too many wrong ones.
    /// </summary>
    public DateTime? LockedUntilUtc { get; set; }
}
