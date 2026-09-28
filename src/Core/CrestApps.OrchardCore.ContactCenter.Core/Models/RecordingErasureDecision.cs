namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a right-to-erasure request against a captured recording, describing whether the
/// recording reference was erased and, when refused, the machine-readable reason.
/// </summary>
public sealed class RecordingErasureDecision
{
    /// <summary>
    /// Gets a value indicating whether the recording reference was erased.
    /// </summary>
    public bool Erased { get; private init; }

    /// <summary>
    /// Gets the stable machine-readable reason code describing why erasure was denied, or <see langword="null"/>
    /// when the recording was erased.
    /// </summary>
    public string DenyReasonCode { get; private init; }

    /// <summary>
    /// Creates a decision that indicates the recording reference was erased.
    /// </summary>
    /// <returns>A decision that indicates erasure completed.</returns>
    public static RecordingErasureDecision Erase()
        => new()
        {
            Erased = true,
        };

    /// <summary>
    /// Creates a decision that denies erasure with the specified reason code.
    /// </summary>
    /// <param name="denyReasonCode">The stable machine-readable reason code describing why erasure was denied.</param>
    /// <returns>A decision that denies erasure.</returns>
    public static RecordingErasureDecision Deny(string denyReasonCode)
        => new()
        {
            Erased = false,
            DenyReasonCode = denyReasonCode,
        };
}
