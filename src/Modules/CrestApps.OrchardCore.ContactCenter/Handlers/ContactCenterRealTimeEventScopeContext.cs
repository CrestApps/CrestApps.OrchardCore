using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Security;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.Core.Telephony;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

internal sealed class ContactCenterRealTimeEventScopeContext
{
    public ContactCenterRealTimeEventScopeContext(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IQueueItemStore queueItemStore,
        IOmnichannelActivityManager activityManager,
        IInteractionManager interactionManager,
        IUserDirectory userDirectory,
        IEnumerable<IIncomingCallDispatcher> incomingCallDispatchers)
    {
        AgentManager = agentManager;
        ReservationManager = reservationManager;
        QueueItemStore = queueItemStore;
        ActivityManager = activityManager;
        InteractionManager = interactionManager;
        UserDirectory = userDirectory;

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

    public IUserDirectory UserDirectory { get; }

    public IIncomingCallDispatcher IncomingCallDispatcher { get; }
}
