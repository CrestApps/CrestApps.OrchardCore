namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What one over-dial cycle measured and decided, kept on <see cref="PredictivePacingState"/> so the reason a campaign
/// dials as it does can be read back.
/// </summary>
public sealed class PredictivePacingSnapshot
{
    /// <summary>
    /// Gets or sets how the cycle paced.
    /// </summary>
    public PredictivePacingDecisionMode Mode { get; set; }

    /// <summary>
    /// Gets or sets why it paced that way.
    /// </summary>
    public PredictivePacingReason Reason { get; set; }

    /// <summary>
    /// Gets or sets the agents free to take a call when the cycle ran.
    /// </summary>
    public int AvailableAgents { get; set; }

    /// <summary>
    /// Gets or sets the busy agents expected to be free within the ring horizon.
    /// </summary>
    public int FreeingAgents { get; set; }

    /// <summary>
    /// Gets or sets the calls already placed without an agent and not yet connected.
    /// </summary>
    public int CallsInFlight { get; set; }

    /// <summary>
    /// Gets or sets the measured answer rate, from 0 to 1.
    /// </summary>
    public double? AnswerRate { get; set; }

    /// <summary>
    /// Gets or sets the rolling abandonment rate, in percent.
    /// </summary>
    public double? AbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate over the compliance window, in percent.
    /// </summary>
    public double? ComplianceAbandonmentRatePercent { get; set; }

    /// <summary>
    /// Gets or sets the calls the cycle wanted in flight.
    /// </summary>
    public int TargetCalls { get; set; }

    /// <summary>
    /// Gets or sets the calls the calculation allowed the cycle to place.
    /// </summary>
    public int DialCount { get; set; }

    /// <summary>
    /// Gets or sets the calls the cycle actually placed.
    /// </summary>
    public int Dialed { get; set; }

    /// <summary>
    /// Gets or sets the share of the over-dial left by the abandonment throttle, from 0 to 1.
    /// </summary>
    public double Throttle { get; set; }

    /// <summary>
    /// Gets or sets the abandonment rate the calculation expects from the calls in flight, in percent.
    /// </summary>
    public double ExpectedAbandonmentPercent { get; set; }
}
