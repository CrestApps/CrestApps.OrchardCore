namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of an action on a secure capture, such as a customer submitting sensitive data or an
/// agent cancelling the capture. It never carries a raw value; on success it reports only that the action was
/// applied.
/// </summary>
public sealed class SecureCaptureActionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the action was applied.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a safe explanation of the outcome, on failure.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="SecureCaptureActionResult"/>.</returns>
    public static SecureCaptureActionResult Success()
        => new() { Succeeded = true };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The safe failure reason.</param>
    /// <returns>A failed <see cref="SecureCaptureActionResult"/>.</returns>
    public static SecureCaptureActionResult Failure(string reason)
        => new() { Succeeded = false, Reason = reason };
}
