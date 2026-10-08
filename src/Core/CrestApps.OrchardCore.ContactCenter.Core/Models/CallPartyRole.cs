namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies the part a party plays in a call. The role is what makes a topology readable without
/// knowing a provider's naming: it distinguishes the customer from the handling agent, a consulted
/// destination, and a supervisor who is present but is not handling the work.
/// </summary>
public enum CallPartyRole
{
    /// <summary>
    /// The party's role has not been determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// The external party the contact center is serving.
    /// </summary>
    Customer,

    /// <summary>
    /// The agent handling the work.
    /// </summary>
    Agent,

    /// <summary>
    /// A destination an agent consulted before completing or abandoning a transfer.
    /// </summary>
    Consult,

    /// <summary>
    /// A supervisor engaged on the call who is not handling the work.
    /// </summary>
    Supervisor,

    /// <summary>
    /// A party outside the contact center added to the call, such as a third-party conference member.
    /// </summary>
    External,
}
