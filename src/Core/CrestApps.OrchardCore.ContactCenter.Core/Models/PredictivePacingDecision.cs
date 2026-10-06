namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of one over-dial calculation: how the campaign dials now, how many calls to place, and the figures the
/// decision was made from, kept so a supervisor can see why.
/// </summary>
public sealed class PredictivePacingDecision
{
    /// <summary>
    /// Gets how the campaign dials after this decision.
    /// </summary>
    public PredictivePacingDecisionMode Mode { get; init; }

    /// <summary>
    /// Gets why the decision came out this way.
    /// </summary>
    public PredictivePacingReason Reason { get; init; }

    /// <summary>
    /// Gets the number of calls to place now without reserving agents. Always zero unless <see cref="Mode"/> is
    /// <see cref="PredictivePacingDecisionMode.OverDial"/>.
    /// </summary>
    public int DialCount { get; init; }

    /// <summary>
    /// Gets the number of calls the campaign should have in flight, the ones already ringing included.
    /// </summary>
    public int TargetCalls { get; init; }

    /// <summary>
    /// Gets the agents counted as able to take a call: those free now plus the credited share of those freeing up.
    /// </summary>
    public double EffectiveAgents { get; init; }

    /// <summary>
    /// Gets the most calls the line limits allowed in flight.
    /// </summary>
    public int CallLimit { get; init; }

    /// <summary>
    /// Gets the answer rate used, after it was bounded to a safe range.
    /// </summary>
    public double AnswerRate { get; init; }

    /// <summary>
    /// Gets the share of the over-dial left after throttling, from 0 (none) to 1 (all of it).
    /// </summary>
    public double Throttle { get; init; }

    /// <summary>
    /// Gets the abandonment rate, in percent, the over-dial was sized to stay under: the target, throttled.
    /// </summary>
    public double EffectiveTargetPercent { get; init; }

    /// <summary>
    /// Gets the abandonment rate, in percent, expected if <see cref="TargetCalls"/> calls are in flight.
    /// </summary>
    public double ExpectedAbandonmentPercent { get; init; }

    /// <summary>
    /// Creates a decision to dial nothing at all.
    /// </summary>
    /// <param name="reason">Why nothing is dialed.</param>
    /// <returns>The decision.</returns>
    public static PredictivePacingDecision Suppressed(PredictivePacingReason reason)
        => new() { Mode = PredictivePacingDecisionMode.Suppressed, Reason = reason };

    /// <summary>
    /// Creates a decision to dial one call per reserved agent rather than over-dial.
    /// </summary>
    /// <param name="reason">Why over-dialing is not safe now.</param>
    /// <param name="throttle">The throttle that was worked out before falling back, if any.</param>
    /// <returns>The decision.</returns>
    public static PredictivePacingDecision ReservedFallback(PredictivePacingReason reason, double throttle = 0)
        => new() { Mode = PredictivePacingDecisionMode.ReservedFallback, Reason = reason, Throttle = throttle };
}
