using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
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
        IInteractionManager interactionManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IEnumerable<IIncomingCallDispatcher> incomingCallDispatchers)
    {
        AgentManager = agentManager;
        ReservationManager = reservationManager;
        QueueItemStore = queueItemStore;
        ActivityManager = activityManager;
        InteractionManager = interactionManager;
        UserManager = userManager;
        DisplayNameProvider = displayNameProvider;

        // The soft-phone incoming-call dispatcher lives in the Telephony module. Real-Time can run without
        // it (a chat-only contact center), so it is resolved optionally; when Telephony is absent the queue
        // ring simply is not projected onto the soft phone.
        IncomingCallDispatcher = incomingCallDispatchers.FirstOrDefault();
    }

    public IAgentProfileManager AgentManager { get; }

    public IActivityReservationManager ReservationManager { get; }

    public IQueueItemStore QueueItemStore { get; }

    public IOmnichannelActivityManager ActivityManager { get; }

    public IInteractionManager InteractionManager { get; }

    public UserManager<IUser> UserManager { get; }

    public IDisplayNameProvider DisplayNameProvider { get; }

    public IIncomingCallDispatcher IncomingCallDispatcher { get; }
}
