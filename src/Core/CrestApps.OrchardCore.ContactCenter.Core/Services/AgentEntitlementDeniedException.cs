namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The exception thrown when an agent attempts to sign in to, or remain a member of, every requested
/// queue and campaign without holding a manager-owned entitlement for any of them.
/// </summary>
public sealed class AgentEntitlementDeniedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentEntitlementDeniedException"/> class.
    /// </summary>
    /// <param name="userId">The Orchard user identifier that was denied.</param>
    public AgentEntitlementDeniedException(string userId)
        : base("You are not entitled to sign in to any of the requested queues or campaigns. Ask a manager to grant queue or campaign entitlements first.")
    {
        UserId = userId;
    }

    /// <summary>
    /// Gets the Orchard user identifier that was denied.
    /// </summary>
    public string UserId { get; }
}
