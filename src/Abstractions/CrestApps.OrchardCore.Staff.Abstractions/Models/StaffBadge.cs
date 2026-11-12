namespace CrestApps.OrchardCore.Staff.Models;

/// <summary>
/// A staff member's PIN for signing in with their badge (the employee number) at a kiosk or register, kept in the staff
/// member's properties by the Badges feature. Only a salted hash is stored.
/// </summary>
public sealed class StaffBadge
{
    /// <summary>
    /// Gets or sets the PBKDF2 hash of the PIN.
    /// </summary>
    public string PinHash { get; set; }

    /// <summary>
    /// Gets or sets the salt of the PIN hash.
    /// </summary>
    public string PinSalt { get; set; }

    /// <summary>
    /// Gets or sets the wrong PINs entered in a row.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the badge is locked until after too many wrong PINs.
    /// </summary>
    public DateTime? LockedUntilUtc { get; set; }
}
