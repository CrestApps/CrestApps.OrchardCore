using CrestApps.Core.Hosting;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Hubs;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;

/// <summary>
/// The Orchard Core endpoint for the real-time SMS portal.
/// </summary>
/// <remarks>
/// The portal itself is <see cref="SmsPortalHubBase"/>. This exists to carry the host's authentication
/// requirement and to keep the type name the browser clients connect to, since a hub's route is derived from
/// its name. It overrides nothing: everything the portal needs from the host it asks for as an authorization
/// operation or a tenant name, both of which this host already answers.
/// </remarks>
[Authorize]
public sealed class SmsPortalHub : SmsPortalHubBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsPortalHub"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the connected agent.</param>
    /// <param name="authorizationService">The authorization service the portal operations are asked through.</param>
    /// <param name="presenceTracker">The tracker that records the connected agent's portal as open.</param>
    /// <param name="tenantAccessor">Names the current tenant.</param>
    public SmsPortalHub(
        IAgentProfileManager agentProfileManager,
        IAuthorizationService authorizationService,
        ISmsAgentPresenceTracker presenceTracker,
        ITenantAccessor tenantAccessor)
        : base(agentProfileManager, authorizationService, presenceTracker, tenantAccessor)
    {
    }
}
