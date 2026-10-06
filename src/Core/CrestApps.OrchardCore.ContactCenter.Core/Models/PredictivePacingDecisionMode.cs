namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How a Predictive campaign dials after one pacing decision.
/// </summary>
public enum PredictivePacingDecisionMode
{
    /// <summary>
    /// Nothing is dialed: the abandonment policy does not permit the profile to dial.
    /// </summary>
    Suppressed = 0,

    /// <summary>
    /// No call is placed without an agent; the campaign dials one call per reserved agent, which cannot abandon, because
    /// over-dialing could not be shown to be safe.
    /// </summary>
    ReservedFallback = 1,

    /// <summary>
    /// The decision's dial count is placed without reserving agents.
    /// </summary>
    OverDial = 2,
}
