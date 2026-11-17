namespace CrestApps.OrchardCore.Banking.Disbursements.Models;

/// <summary>
/// What a provider did with a batch.
/// </summary>
public sealed class DisbursementSubmitResult
{
    /// <summary>
    /// Gets or sets whether the batch was sent.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets why it could not be sent.
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the file the provider wrote, if any.
    /// </summary>
    public byte[] File { get; set; }

    /// <summary>
    /// Gets or sets the name of that file.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the provider's reference of the batch.
    /// </summary>
    public string ProviderReference { get; set; }

    /// <summary>
    /// Gets or sets the status each disbursement moves to: Submitted when it was sent, Settled when the provider paid it
    /// at once, Failed when it refused one. A disbursement that is not listed moves to Submitted.
    /// </summary>
    public Dictionary<string, DisbursementStatus> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the trace number or reference of each disbursement, by disbursement id.
    /// </summary>
    public Dictionary<string, string> TraceNumbers { get; set; } = [];

    /// <summary>
    /// Gets or sets why each failed disbursement failed, by disbursement id.
    /// </summary>
    public Dictionary<string, string> FailureReasons { get; set; } = [];

    /// <summary>
    /// Gets or sets the check that pays each disbursement, by disbursement id, when the provider issued checks.
    /// </summary>
    public Dictionary<string, string> CheckIds { get; set; } = [];

    /// <summary>
    /// Returns a failed result.
    /// </summary>
    /// <param name="errors">Why.</param>
    /// <returns>The result.</returns>
    public static DisbursementSubmitResult Failed(params string[] errors)
        => new() { Succeeded = false, Errors = errors };
}
