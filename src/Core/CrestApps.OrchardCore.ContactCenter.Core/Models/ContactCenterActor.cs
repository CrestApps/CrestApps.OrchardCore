using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Who made a recorded change: the kind of actor and, when there is one, their identifier.
/// </summary>
/// <param name="Type">The kind of actor.</param>
/// <param name="Id">The actor's identifier: a user id for an agent or supervisor, a workflow id, a provider name, or
/// <see langword="null"/> for the platform.</param>
public sealed record ContactCenterActor(ContactCenterActorType Type, string Id = null)
{
    /// <summary>
    /// The platform acting on its own: routing, a sweep, a timeout or reconciliation.
    /// </summary>
    public static ContactCenterActor System { get; } = new(ContactCenterActorType.System);

    /// <summary>
    /// Creates the actor for an agent acting for themselves.
    /// </summary>
    /// <param name="userId">The agent's user id.</param>
    /// <returns>The actor.</returns>
    public static ContactCenterActor Agent(string userId) => new(ContactCenterActorType.Agent, userId);

    /// <summary>
    /// Creates the actor for a supervisor.
    /// </summary>
    /// <param name="userId">The supervisor's user id.</param>
    /// <returns>The actor.</returns>
    public static ContactCenterActor Supervisor(string userId) => new(ContactCenterActorType.Supervisor, userId);

    /// <summary>
    /// Creates the actor for a workflow.
    /// </summary>
    /// <param name="workflowId">The workflow's identifier.</param>
    /// <returns>The actor.</returns>
    public static ContactCenterActor Workflow(string workflowId) => new(ContactCenterActorType.Workflow, workflowId);

    /// <summary>
    /// Creates the actor for the telephony provider.
    /// </summary>
    /// <param name="providerName">The provider's technical name.</param>
    /// <returns>The actor.</returns>
    public static ContactCenterActor Provider(string providerName) => new(ContactCenterActorType.Provider, providerName);
}
