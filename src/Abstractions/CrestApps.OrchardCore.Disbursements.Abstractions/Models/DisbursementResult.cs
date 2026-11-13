namespace CrestApps.OrchardCore.Disbursements.Models;

/// <summary>
/// The outcome of recording a disbursement.
/// </summary>
public sealed class DisbursementResult
{
    /// <summary>
    /// Gets or sets the disbursement, new or the one the idempotency key already gave.
    /// </summary>
    public Disbursement Disbursement { get; set; }

    /// <summary>
    /// Gets or sets whether the key already gave this disbursement, so nothing new was recorded.
    /// </summary>
    public bool AlreadyExisted { get; set; }

    /// <summary>
    /// Gets or sets why it was refused.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets whether a disbursement was recorded or found.
    /// </summary>
    public bool Succeeded
        => Disbursement is not null && Errors.Count == 0;
}
