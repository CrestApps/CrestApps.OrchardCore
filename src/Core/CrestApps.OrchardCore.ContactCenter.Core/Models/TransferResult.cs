namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a transfer request.
/// </summary>
public sealed class TransferResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the transfer was accepted and recorded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider may have executed the transfer but its
    /// outcome could not be observed.
    /// </summary>
    public bool OutcomeUnknown { get; set; }

    /// <summary>
    /// Gets or sets an explanation of the outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="reason">The optional explanation.</param>
    /// <returns>A successful <see cref="TransferResult"/>.</returns>
    public static TransferResult Success(string reason = null)
    {
        return new TransferResult { Succeeded = true, Reason = reason };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>A failed <see cref="TransferResult"/>.</returns>
    public static TransferResult Failure(string reason)
    {
        return new TransferResult { Succeeded = false, Reason = reason };
    }

    /// <summary>
    /// Creates a result for a transfer whose provider outcome could not be determined.
    /// </summary>
    /// <param name="reason">The reason the outcome is unknown.</param>
    /// <returns>An indeterminate <see cref="TransferResult"/>.</returns>
    public static TransferResult Unknown(string reason)
    {
        return new TransferResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            Reason = reason,
        };
    }
}
