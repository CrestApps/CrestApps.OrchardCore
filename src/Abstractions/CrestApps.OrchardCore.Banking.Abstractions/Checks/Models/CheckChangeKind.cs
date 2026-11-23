namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// What happened to a check.
/// </summary>
public enum CheckChangeKind
{
    /// <summary>
    /// The check was issued with its number.
    /// </summary>
    Issued,

    /// <summary>
    /// The check was printed.
    /// </summary>
    Printed,

    /// <summary>
    /// The check was voided.
    /// </summary>
    Voided,

    /// <summary>
    /// The bank was asked not to pay it.
    /// </summary>
    StopPayment,

    /// <summary>
    /// The bank paid it.
    /// </summary>
    Cleared,

    /// <summary>
    /// Another check replaced it.
    /// </summary>
    Reissued,

    /// <summary>
    /// It became too old to pay.
    /// </summary>
    Stale,

    /// <summary>
    /// A printed check was printed again, with a reason, such as after a printer jam.
    /// </summary>
    Reprinted,

    /// <summary>
    /// A cleared check was recorded as not paid after all, such as a clearing matched to the wrong check: it went back
    /// to the status it cleared from.
    /// </summary>
    Uncleared,

    /// <summary>
    /// A sheet of pre-printed stock misprinted in a print run: its number was kept as a new voided check
    /// (<see cref="CheckRecord.IsSpoiled"/>), and the check it was printed for waits to print again.
    /// </summary>
    Spoiled,
}
