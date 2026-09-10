namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies who initiated a call-control operation, which selects the authorization rule applied to it.
/// </summary>
/// <remarks>
/// The default value is <see cref="Agent"/> so that a context or durable payload which never sets an
/// initiator is authorized as an agent request and therefore fails closed without an owning agent.
/// </remarks>
public enum CallControlInitiator
{
    /// <summary>
    /// The operation was requested by an authenticated agent or supervisor and must pass an ownership check.
    /// </summary>
    Agent,

    /// <summary>
    /// The operation was issued by the platform itself with no requesting principal, such as terminating an
    /// inbound call that arrived at a closed entry point or an unroutable queue.
    /// </summary>
    System,
}
