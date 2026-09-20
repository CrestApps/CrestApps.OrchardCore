using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Hubs;
using CrestApps.Core.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// The Orchard Core endpoint for the real-time Contact Center.
/// </summary>
/// <remarks>
/// The Contact Center itself is <see cref="ContactCenterHubBase"/>. This exists to carry the host's
/// authentication requirement and to keep the type name the browser clients connect to, since a hub's
/// route is derived from its name. It overrides nothing: everything the hub needs from the host it asks
/// for as an authorization operation, a tenant name or a unit of work, all of which this host answers.
/// </remarks>
[Authorize]
public sealed class ContactCenterHub : ContactCenterHubBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeExecutor">The executor used to isolate hub operations in child shell scopes.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="connectionRegistry">The tenant-local hub connection registry.</param>
    /// <param name="tenantAccessor">Names the current tenant.</param>
    public ContactCenterHub(
        ILogger<ContactCenterHub> logger,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterFeatureWorkManager workManager,
        ContactCenterHubConnectionRegistry connectionRegistry,
        ITenantAccessor tenantAccessor)
        : base(logger, scopeExecutor, workManager, connectionRegistry, tenantAccessor)
    {
    }
}
