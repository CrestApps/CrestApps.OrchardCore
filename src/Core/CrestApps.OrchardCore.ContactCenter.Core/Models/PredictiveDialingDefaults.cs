namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The defaults and the allowed ranges of a Predictive dialer profile's pacing settings, shared by the profile, its
/// validation, its editor and the over-dial calculator so they cannot disagree.
/// </summary>
public static class PredictiveDialingDefaults
{
    /// <summary>
    /// The abandonment rate, in percent of calls a person answered, a new profile steers toward.
    /// </summary>
    public const double TargetAbandonmentRatePercent = 2;

    /// <summary>
    /// The most calls per available agent a new profile may have ringing.
    /// </summary>
    public const double LinesPerAgent = 2;

    /// <summary>
    /// The fewest calls per available agent a profile may be set to: one, which is what reserving an agent per call
    /// places anyway.
    /// </summary>
    public const double MinLinesPerAgent = 1;

    /// <summary>
    /// The most calls per available agent any profile may have ringing, however it is configured. Beyond it a wrong
    /// answer-rate measurement abandons calls faster than any rolling window could notice.
    /// </summary>
    public const double MaxLinesPerAgent = 5;

    /// <summary>
    /// The most calls a new profile may have in flight for one campaign.
    /// </summary>
    public const int CallsInFlight = 100;

    /// <summary>
    /// The most calls in flight any profile may be set to.
    /// </summary>
    public const int MaxCallsInFlight = 1000;

    /// <summary>
    /// The fewest settled calls a new profile measures its answer rate over before it trusts it.
    /// </summary>
    public const int AnswerRateSampleFloor = 50;

    /// <summary>
    /// The lowest answer-rate sample floor a profile may be set to. Fewer calls than this say too little about the next
    /// ones to size an over-dial from.
    /// </summary>
    public const int MinAnswerRateSampleFloor = 10;

    /// <summary>
    /// The highest answer-rate sample floor a profile may be set to.
    /// </summary>
    public const int MaxAnswerRateSampleFloor = 10000;

    /// <summary>
    /// The minutes of history a new profile measures its answer rate over.
    /// </summary>
    public const int AnswerRateWindowMinutes = 15;

    /// <summary>
    /// The shortest answer-rate window a profile may be set to, in minutes.
    /// </summary>
    public const int MinAnswerRateWindowMinutes = 5;

    /// <summary>
    /// The longest answer-rate window a profile may be set to, in minutes.
    /// </summary>
    public const int MaxAnswerRateWindowMinutes = 240;

    /// <summary>
    /// The percentage of the agents expected to free up a new profile counts when it credits them.
    /// </summary>
    public const int FreeUpCreditPercent = 50;

    /// <summary>
    /// The longest a person who answered may be kept waiting for an agent to free up, in milliseconds. It is well inside
    /// the two seconds after which the call counts as abandoned, because connecting the agent takes time of its own.
    /// </summary>
    public const int MaxConnectWaitMilliseconds = 1500;
}
