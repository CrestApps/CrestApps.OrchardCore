namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Everything one over-dial decision is made from: what is measured now (agents, calls in flight, rates) and the limits
/// the campaign's dialer profile and the tenant set.
/// </summary>
/// <remarks>
/// A measurement that could not be taken is <see langword="null"/>, never a guess: the calculator refuses to over-dial on
/// a missing value.
/// </remarks>
public sealed class PredictivePacingInput
{
    /// <summary>
    /// Gets or sets a value indicating whether the abandonment policy permits the profile to dial at all.
    /// </summary>
    public bool PolicyPermitted { get; set; }

    /// <summary>
    /// Gets or sets the number of agents free to take a call now.
    /// </summary>
    public int AvailableAgents { get; set; }

    /// <summary>
    /// Gets or sets the number of busy agents expected to be free before a call placed now is answered.
    /// </summary>
    public int FreeingAgents { get; set; }

    /// <summary>
    /// Gets or sets the number of the campaign's calls already placed and not yet connected to an agent or ended,
    /// including calls a person answered that are waiting for an agent.
    /// </summary>
    public int CallsInFlight { get; set; }

    /// <summary>
    /// Gets or sets the measured share of calls a person answers, from 0 to 1, or <see langword="null"/> when unknown.
    /// </summary>
    public double? AnswerRate { get; set; }

    /// <summary>
    /// Gets or sets the number of settled calls <see cref="AnswerRate"/> is measured from.
    /// </summary>
    public long AnswerRateSampleSize { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate over the profile's rolling window, in percent of calls a person answered, or
    /// <see langword="null"/> when unknown.
    /// </summary>
    public double? AbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the number of calls a person answered that <see cref="AbandonmentRatePercent"/> is measured from.
    /// </summary>
    public long AbandonmentSampleSize { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate over the long compliance window (thirty days by default), in percent of calls a
    /// person answered, or <see langword="null"/> when unknown.
    /// </summary>
    public double? ComplianceAbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate the over-dial steers toward, in percent.
    /// </summary>
    public double TargetAbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate the profile must never reach, in percent. Zero or less means the profile enforces
    /// no cap, and a profile without a cap is never over-dialed.
    /// </summary>
    public double MaxAbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the fewest settled calls <see cref="AnswerRate"/> must be measured from.
    /// </summary>
    public int AnswerRateSampleFloor { get; set; }

    /// <summary>
    /// Gets or sets the fewest answered calls <see cref="AbandonmentRatePercent"/> must be measured from.
    /// </summary>
    public int AbandonmentSampleFloor { get; set; }

    /// <summary>
    /// Gets or sets the most calls that may ring for each available agent.
    /// </summary>
    public double MaxLinesPerAgent { get; set; }

    /// <summary>
    /// Gets or sets the most calls the campaign may have in flight.
    /// </summary>
    public int MaxCallsInFlight { get; set; }

    /// <summary>
    /// Gets or sets the most calls one decision may place.
    /// </summary>
    public int MaxDialsPerCycle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether agents expected to free up are counted.
    /// </summary>
    public bool CreditAgentsFreeingUp { get; set; }

    /// <summary>
    /// Gets or sets the percentage of <see cref="FreeingAgents"/> counted when <see cref="CreditAgentsFreeingUp"/> is set.
    /// </summary>
    public int FreeUpCreditPercent { get; set; }

    /// <summary>
    /// Creates an input carrying a dialer profile's and the tenant's limits, for the caller to add the measurements to.
    /// </summary>
    /// <param name="profile">The dialer profile being paced.</param>
    /// <param name="options">The tenant's predictive dialing options.</param>
    /// <returns>The input, with every measurement still unknown.</returns>
    public static PredictivePacingInput ForProfile(DialerProfile profile, ContactCenterPredictiveDialingOptions options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);

        return new PredictivePacingInput
        {
            TargetAbandonmentRatePercent = profile.TargetAbandonmentRatePercent,

            // A profile that does not enforce its cap has no limit to steer under, and is never over-dialed.
            MaxAbandonmentRatePercent = profile.EnforceAbandonmentCap ? profile.MaxAbandonmentRatePercent : 0,
            AnswerRateSampleFloor = profile.AnswerRateSampleFloor,
            AbandonmentSampleFloor = profile.AbandonmentSampleFloor,
            MaxLinesPerAgent = profile.MaxLinesPerAgent,
            MaxCallsInFlight = profile.MaxCallsInFlight,
            MaxDialsPerCycle = options.MaxDialsPerCycle,
            CreditAgentsFreeingUp = profile.CreditAgentsFreeingUp,
            FreeUpCreditPercent = profile.FreeUpCreditPercent,
        };
    }
}
