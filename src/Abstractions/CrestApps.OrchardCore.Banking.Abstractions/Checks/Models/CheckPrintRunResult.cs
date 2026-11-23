namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// What a confirmed print run recorded, or why it was refused.
/// </summary>
public sealed class CheckPrintRunResult
{
    /// <summary>
    /// Gets or sets the checks recorded as printed, with their numbers.
    /// </summary>
    public IReadOnlyList<CheckRecord> Printed { get; set; } = [];

    /// <summary>
    /// Gets or sets the spoiled sheets kept as voided checks, one per misprint.
    /// </summary>
    public IReadOnlyList<CheckRecord> Spoiled { get; set; } = [];

    /// <summary>
    /// Gets or sets why the run was refused; nothing was saved then.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether the run was recorded.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;
}
