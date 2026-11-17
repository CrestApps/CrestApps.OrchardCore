namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// The outcome of issuing or reissuing a check.
/// </summary>
public sealed class CheckResult
{
    /// <summary>
    /// Gets or sets the check, when one was written.
    /// </summary>
    public CheckRecord Check { get; set; }

    /// <summary>
    /// Gets or sets why no check was written.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether a check was written.
    /// </summary>
    public bool Succeeded
        => Check is not null && Errors.Count == 0;
}
