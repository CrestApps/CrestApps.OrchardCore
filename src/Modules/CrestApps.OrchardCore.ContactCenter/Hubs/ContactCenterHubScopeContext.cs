using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

internal sealed class ContactCenterHubScopeContext
{
    public ContactCenterHubScopeContext(
        IAuthorizationService authorizationService,
        IAgentSessionService sessionService,
        IAgentPresenceManager presenceManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IEnumerable<IQueuedVoiceWorkOfferService> queuedVoiceWorkOfferServices,
        IEnumerable<IPendingIncomingCallOfferService> pendingIncomingCallOfferServices)
    {
        AuthorizationService = authorizationService;
        SessionService = sessionService;
        PresenceManager = presenceManager;
        SupervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        UserManager = userManager;
        DisplayNameProvider = displayNameProvider;
        QueuedVoiceWorkOfferService = queuedVoiceWorkOfferServices.FirstOrDefault();
        PendingIncomingCallOfferService = pendingIncomingCallOfferServices.FirstOrDefault();
    }

    public IAuthorizationService AuthorizationService { get; }

    public IAgentSessionService SessionService { get; }

    public IAgentPresenceManager PresenceManager { get; }

    public ISupervisorQueueAuthorizationService SupervisorQueueAuthorizationService { get; }

    public UserManager<IUser> UserManager { get; }

    public IDisplayNameProvider DisplayNameProvider { get; }

    public IQueuedVoiceWorkOfferService QueuedVoiceWorkOfferService { get; }

    public IPendingIncomingCallOfferService PendingIncomingCallOfferService { get; }
}
