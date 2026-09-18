using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace CrestApps.Core.ContactCenter.Security;

/// <summary>
/// What a Contact Center service asks the host to authorize.
/// </summary>
/// <remarks>
/// <para>
/// An operation says what the caller is trying to do; the host decides who may do it. That keeps the
/// suite from naming the host's permissions, which are the host's to define, rename and group.
/// </para>
/// <para>
/// One entry per question a Contact Center service actually asks. A host must supply a handler for
/// every operation here, because an unhandled requirement is a denial: a missing registration takes
/// the feature away rather than opening it up.
/// </para>
/// </remarks>
public static class ContactCenterOperations
{
    /// <summary>
    /// Watch and act on work belonging to a queue the caller does not own.
    /// </summary>
    /// <remarks>
    /// The resource is the queue identifier. The host answers the coarse question - may this caller
    /// supervise at all - and the suite then narrows it to the queues that caller is entitled to.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement SuperviseQueue = new() { Name = nameof(SuperviseQueue) };

    /// <summary>
    /// Send an interaction to a destination outside the organization.
    /// </summary>
    /// <remarks>
    /// The resource is the identifier of the approved destination being requested. Separate from
    /// ordinary transfer because the cost of getting it wrong is a call leaving the building.
    /// </remarks>
    public static readonly OperationAuthorizationRequirement TransferExternally = new() { Name = nameof(TransferExternally) };
}
