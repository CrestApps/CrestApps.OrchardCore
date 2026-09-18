using CrestApps.Core.Security;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

internal sealed class ContactCenterHubScopeContext
{
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

    public IAuthorizationService AuthorizationService { get; }

    public IAgentSessionService SessionService { get; }

    public IAgentPresenceManager PresenceManager { get; }

    public ISupervisorQueueAuthorizationService SupervisorQueueAuthorizationService { get; }

    public IUserDirectory UserDirectory { get; }

    public IQueuedVoiceWorkOfferService QueuedVoiceWorkOfferService { get; }

    public IPendingIncomingCallOfferService PendingIncomingCallOfferService { get; }
}
