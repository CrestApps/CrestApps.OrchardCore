namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// A print run of queued checks on pre-printed stock, as the person confirms it after printing: the checks in the order
/// they printed, the number of the first sheet, and the sheets that misprinted.
/// </summary>
public sealed class CheckPrintRunRequest
{
    /// <summary>
    /// Gets or sets the checks, in the order they printed, all <see cref="CheckStatus.Queued"/> and drawn on one bank
    /// account. The first gets <see cref="FirstNumber"/>, the next one more, and so on.
    /// </summary>
    public IReadOnlyList<CheckRecord> Checks { get; set; } = [];

    /// <summary>
    /// Gets or sets the number printed on the first sheet, 1 or more. No check of the account may have any of the
    /// numbers the run uses.
    /// </summary>
    public long FirstNumber { get; set; }

    /// <summary>
    /// Gets or sets the ids of the checks whose sheets misprinted. Each sheet's number is kept as a spoiled, voided
    /// check, and the check waits to print again.
    /// </summary>
    public IReadOnlyCollection<string> MisprintedCheckIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the print run's id, stored on each check and each spoiled sheet.
    /// </summary>
    public string PrintRunId { get; set; }
}
