using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// The services a supervisor's soft phone reaches through the hub to change or stop their own engagement. Monitoring
/// is part of Contact Center Voice, so it is optional here: without it there is nothing to change.
/// </summary>
internal sealed class ContactCenterSupervisionHubScopeContext
{
    public ContactCenterSupervisionHubScopeContext(
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IInteractionManager interactionManager,
        IEnumerable<ICallSessionManager> callSessionManagers,
        IEnumerable<IContactCenterMonitoringService> monitoringServices,
        IEnumerable<IContactCenterPhoneCallSupervisionService> phoneCallServices)
    {
        AuthorizationService = authorizationService;
        SupervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        InteractionManager = interactionManager;
        CallSessionManager = callSessionManagers.FirstOrDefault();
        MonitoringService = monitoringServices.FirstOrDefault();
        PhoneCalls = phoneCallServices.FirstOrDefault();
    }

    public IAuthorizationService AuthorizationService { get; }

    public ISupervisorQueueAuthorizationService SupervisorQueueAuthorizationService { get; }

    public IInteractionManager InteractionManager { get; }

    public ICallSessionManager CallSessionManager { get; }

    public IContactCenterMonitoringService MonitoringService { get; }

    // An agent's own phone call, which the supervisor's phone names with a PhoneCallKey; part of the live dashboard.
    public IContactCenterPhoneCallSupervisionService PhoneCalls { get; }
}
