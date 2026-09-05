namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Decides whether the platform is willing to place or transfer a call to an address. This is a safety policy,
/// not a formatting concern: the question is asked when an administrator saves a destination, when a transfer is
/// resolved at call time, and when a dial command is executed, and an address refused at one of those moments
/// must be refused at all of them.
/// </summary>
public interface IDialDestinationPolicy
{
    /// <summary>
    /// Evaluates an address against the policy.
    /// </summary>
    /// <param name="address">The raw address the caller wants to reach.</param>
    /// <param name="context">What is known about the attempt beyond the address.</param>
    /// <returns>The decision, carrying a reason when the address is refused.</returns>
    DialDestinationDecision Evaluate(string address, DialDestinationContext context);
}
