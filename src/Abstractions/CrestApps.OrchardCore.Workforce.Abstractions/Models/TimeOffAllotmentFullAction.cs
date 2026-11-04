namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What happens to a request when as many people as an allotment allows are already off.
/// </summary>
public enum TimeOffAllotmentFullAction
{
    /// <summary>
    /// The request waits for a place and is approved, in the order requests came in, when one frees up.
    /// </summary>
    Waitlist,

    /// <summary>
    /// The request goes to a manager, who can approve it anyway.
    /// </summary>
    Pending,
}
