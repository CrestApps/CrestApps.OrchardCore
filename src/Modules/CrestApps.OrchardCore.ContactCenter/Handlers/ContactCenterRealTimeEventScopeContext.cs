using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ContactCenterRealTimeEventScopeContext
{
    public ContactCenterRealTimeEventScopeContext(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IQueueItemStore queueItemStore,
        IOmnichannelActivityManager activityManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        AgentManager = agentManager;
        ReservationManager = reservationManager;
        QueueItemStore = queueItemStore;
        ActivityManager = activityManager;
        UserManager = userManager;
        DisplayNameProvider = displayNameProvider;
    }

    public IAgentProfileManager AgentManager { get; }

    public IActivityReservationManager ReservationManager { get; }

    public IQueueItemStore QueueItemStore { get; }

    public IOmnichannelActivityManager ActivityManager { get; }

    public UserManager<IUser> UserManager { get; }

    public IDisplayNameProvider DisplayNameProvider { get; }
}
