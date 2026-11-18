namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// Where the check sits on a letter-size sheet, and how many there are.
/// </summary>
public enum CheckForm
{
    /// <summary>
    /// The check on the top third, two vouchers below it.
    /// </summary>
    CheckOnTop,

    /// <summary>
    /// The check on the middle third, a voucher above and below.
    /// </summary>
    CheckInMiddle,

    /// <summary>
    /// The check on the bottom third, two vouchers above it.
    /// </summary>
    CheckOnBottom,

    /// <summary>
    /// Three checks on a sheet, without vouchers.
    /// </summary>
    ThreePerPage,
}
