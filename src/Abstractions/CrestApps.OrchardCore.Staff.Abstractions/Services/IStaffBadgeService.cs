using CrestApps.OrchardCore.Staff.Models;

namespace CrestApps.OrchardCore.Staff.Services;

/// <summary>
/// Signs staff in with a badge (the employee number) and a PIN, at a time clock kiosk or a register. PINs are stored as
/// salted PBKDF2 hashes and a badge locks for a while after too many wrong PINs.
/// </summary>
public interface IStaffBadgeService
{
    /// <summary>
    /// Sets or replaces a staff member's PIN. The staff member needs an employee number. Save the staff member afterwards.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <param name="pin">The PIN: 4 to 8 digits.</param>
    /// <returns>An error message, or <see langword="null"/> when the PIN was set.</returns>
    string SetPin(StaffMember staffMember, string pin);

    /// <summary>
    /// Removes a staff member's PIN. Save the staff member afterwards.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    void ClearPin(StaffMember staffMember);

    /// <summary>
    /// Gets whether a staff member has a PIN.
    /// </summary>
    /// <param name="staffMember">The staff member.</param>
    /// <returns><see langword="true"/> when a PIN is set.</returns>
    bool HasPin(StaffMember staffMember);

    /// <summary>
    /// Checks a badge and PIN, counting wrong PINs and locking the badge after too many.
    /// </summary>
    /// <param name="employeeNumber">The badge (employee) number.</param>
    /// <param name="pin">The PIN.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result.</returns>
    Task<StaffBadgeResult> VerifyAsync(string employeeNumber, string pin, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of checking a badge and PIN.
/// </summary>
public sealed class StaffBadgeResult
{
    /// <summary>
    /// Gets or sets whether the badge and PIN are right.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the staff member, when the badge is known.
    /// </summary>
    public StaffMember StaffMember { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the badge is locked until, when it is locked.
    /// </summary>
    public DateTime? LockedUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets why it failed.
    /// </summary>
    public StaffBadgeFailure Failure { get; set; }
}

/// <summary>
/// Why a badge and PIN were refused.
/// </summary>
public enum StaffBadgeFailure
{
    /// <summary>
    /// Not refused.
    /// </summary>
    None,

    /// <summary>
    /// The badge or PIN is wrong, or the badge has no PIN. The same answer for each, so badges can't be guessed.
    /// </summary>
    Invalid,

    /// <summary>
    /// The badge is locked after too many wrong PINs.
    /// </summary>
    Locked,

    /// <summary>
    /// The staff member isn't working.
    /// </summary>
    Inactive,
}
