using CrestApps.Core.Security;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Hubs;

/// <summary>
/// The set of services a single Contact Center hub invocation works through.
/// </summary>
/// <remarks>
/// <para>
/// A hub instance outlives the work it does: it is created when the connection opens and survives
/// every call made over it, while the services below belong to one unit of work. Resolving them as a
/// group, per invocation, keeps a long-lived connection from holding a short-lived service.
/// </para>
/// <para>
/// It is one type rather than seven constructor parameters so that adding a collaborator to the hub
/// does not change the signature the host's scope executor is written against.
/// </para>
/// </remarks>
public sealed class ContactCenterHubScopeContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHubScopeContext"/> class.
    /// </summary>
    /// <param name="authorizationService">The service the hub's operations are asked through.</param>
    /// <param name="sessionService">The agent session service.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="supervisorQueueAuthorizationService">Decides whether a supervisor may watch a queue.</param>
    /// <param name="userDirectory">The directory the connected user's display name is read from.</param>
    /// <param name="queuedVoiceWorkOfferService">Offers already-waiting voice work to an agent.</param>
    /// <param name="pendingIncomingCallOfferServices">
    /// The pending-offer services, taken as a sequence because the capability is optional: a host with
    /// no voice channel registers none, and the hub then has no pending offer to restore.
    /// </param>
    public ContactCenterHubScopeContext(
        IAuthorizationService authorizationService,
        IAgentSessionService sessionService,
        IAgentPresenceManager presenceManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IUserDirectory userDirectory,
        IQueuedVoiceWorkOfferService queuedVoiceWorkOfferService,
        IEnumerable<IPendingIncomingCallOfferService> pendingIncomingCallOfferServices)
    {
        AuthorizationService = authorizationService;
        SessionService = sessionService;
        PresenceManager = presenceManager;
        SupervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        UserDirectory = userDirectory;
        QueuedVoiceWorkOfferService = queuedVoiceWorkOfferService;
        PendingIncomingCallOfferService = pendingIncomingCallOfferServices.FirstOrDefault();
    }

    /// <summary>
    /// Gets the service the hub's operations are asked through.
    /// </summary>
    public IAuthorizationService AuthorizationService { get; }

    /// <summary>
    /// Gets the agent session service.
    /// </summary>
    public IAgentSessionService SessionService { get; }

    /// <summary>
    /// Gets the agent presence manager.
    /// </summary>
    public IAgentPresenceManager PresenceManager { get; }

    /// <summary>
    /// Gets the service that decides whether a supervisor may watch a queue.
    /// </summary>
    public ISupervisorQueueAuthorizationService SupervisorQueueAuthorizationService { get; }

    /// <summary>
    /// Gets the directory the connected user's display name is read from.
    /// </summary>
    public IUserDirectory UserDirectory { get; }

    /// <summary>
    /// Gets the service that offers already-waiting voice work to an agent.
    /// </summary>
    public IQueuedVoiceWorkOfferService QueuedVoiceWorkOfferService { get; }

    /// <summary>
    /// Gets the pending-offer service, or <see langword="null"/> when no voice channel is present.
    /// </summary>
    public IPendingIncomingCallOfferService PendingIncomingCallOfferService { get; }
}
