namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Why a pacing decision came out the way it did.
/// </summary>
public enum PredictivePacingReason
{
    /// <summary>
    /// The over-dial was sized from the measurements.
    /// </summary>
    Paced = 0,

    /// <summary>
    /// The abandonment policy does not permit the profile to dial.
    /// </summary>
    PolicySuppressed = 1,

    /// <summary>
    /// The limits are not ones over-dialing can run under: no abandonment cap, a target at or above the cap, or a limit out
    /// of range.
    /// </summary>
    InvalidSettings = 2,

    /// <summary>
    /// The answer rate could not be measured.
    /// </summary>
    AnswerRateUnavailable = 3,

    /// <summary>
    /// Too few calls have settled to trust the answer rate.
    /// </summary>
    AnswerRateSampleBelowFloor = 4,

    /// <summary>
    /// The rolling abandonment rate could not be measured.
    /// </summary>
    AbandonmentRateUnavailable = 5,

    /// <summary>
    /// Too few calls were answered to trust the rolling abandonment rate.
    /// </summary>
    AbandonmentSampleBelowFloor = 6,

    /// <summary>
    /// The long-run abandonment rate could not be measured.
    /// </summary>
    ComplianceRateUnavailable = 7,

    /// <summary>
    /// The long-run abandonment rate has reached the cap.
    /// </summary>
    ComplianceRateAtCap = 8,

    /// <summary>
    /// The rolling abandonment rate has reached the cap.
    /// </summary>
    RollingRateAtCap = 9,

    /// <summary>
    /// The rolling abandonment rate is close enough to the cap that the over-dial is throttled away entirely.
    /// </summary>
    ThrottledToZero = 10,

    /// <summary>
    /// Fewer than one agent is free, or expected to be, so any call answered now would be abandoned.
    /// </summary>
    NoAgents = 11,
}
