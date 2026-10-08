namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a recording governance evaluation, describing whether recording may proceed and, when
/// permitted, the retention and legal-hold metadata to stamp onto the interaction at capture time.
/// </summary>
public sealed class RecordingGovernanceDecision
{
    /// <summary>
    /// Gets a value indicating whether recording is permitted to proceed.
    /// </summary>
    public bool Allowed { get; private init; }

    /// <summary>
    /// Gets the stable machine-readable reason code describing why recording was denied, or <see langword="null"/>
    /// when recording is permitted.
    /// </summary>
    public string DenyReasonCode { get; private init; }

    /// <summary>
    /// Gets the UTC instant beyond which a captured recording becomes eligible for erasure, or <see langword="null"/>
    /// when no retention window applies. Only meaningful when <see cref="Allowed"/> is <see langword="true"/>.
    /// </summary>
    public DateTime? RetainUntilUtc { get; private init; }

    /// <summary>
    /// Gets a value indicating whether a captured recording should begin under legal hold. Only meaningful when
    /// <see cref="Allowed"/> is <see langword="true"/>.
    /// </summary>
    public bool LegalHold { get; private init; }

    /// <summary>
    /// Creates a decision that permits recording with the resolved retention and legal-hold metadata.
    /// </summary>
    /// <param name="retainUntilUtc">The UTC instant beyond which the recording becomes eligible for erasure, or <see langword="null"/> for indefinite retention.</param>
    /// <param name="legalHold">Whether the captured recording should begin under legal hold.</param>
    /// <returns>A decision that permits recording.</returns>
    public static RecordingGovernanceDecision Allow(DateTime? retainUntilUtc, bool legalHold)
        => new()
        {
            Allowed = true,
            RetainUntilUtc = retainUntilUtc,
            LegalHold = legalHold,
        };

    /// <summary>
    /// Creates a decision that denies recording with the specified reason code.
    /// </summary>
    /// <param name="denyReasonCode">The stable machine-readable reason code describing why recording was denied.</param>
    /// <returns>A decision that denies recording.</returns>
    public static RecordingGovernanceDecision Deny(string denyReasonCode)
        => new()
        {
            Allowed = false,
            DenyReasonCode = denyReasonCode,
        };
}
