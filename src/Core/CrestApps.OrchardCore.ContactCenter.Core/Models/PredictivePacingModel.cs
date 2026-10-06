namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// How a Predictive dialer profile paces its calls.
/// </summary>
public enum PredictivePacingModel
{
    /// <summary>
    /// An agent is reserved before every call is placed, so a person who answers always has an agent waiting for
    /// them. This is the pacing every Predictive profile runs today, and the one an over-dialing profile falls back to
    /// whenever it cannot prove that over-dialing is safe.
    /// </summary>
    ReservedPerCall = 0,

    /// <summary>
    /// More calls are placed than there are free agents, sized from the measured answer rate so that the expected share
    /// of answered calls no agent can take stays under the profile's target abandonment rate. An agent is picked when a
    /// person answers. Not available yet: a profile that selects it dials one call per reserved agent until it is.
    /// </summary>
    OverDial = 1,
}
