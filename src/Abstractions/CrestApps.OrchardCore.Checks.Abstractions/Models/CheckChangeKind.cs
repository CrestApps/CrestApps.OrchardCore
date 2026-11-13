namespace CrestApps.OrchardCore.Checks.Models;

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
}
