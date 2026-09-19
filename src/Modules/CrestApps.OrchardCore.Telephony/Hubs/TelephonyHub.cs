using CrestApps.Core.Hosting;
using CrestApps.OrchardCore.Telephony.Core.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// The Orchard Core endpoint for the soft phone.
/// </summary>
/// <remarks>
/// The soft phone itself is <see cref="TelephonyHubBase"/>. This exists to carry the host's
/// authentication requirement and to keep the type name the browser clients connect to, since a hub's
/// route is derived from its name. It overrides nothing: everything the hub needs from the host it asks
/// for as an authorization operation, a tenant name or a unit of work, all of which this host answers.
/// </remarks>
[Authorize]
public sealed class TelephonyHub : TelephonyHubBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="scopeExecutor">The executor used to isolate hub operations in child shell scopes.</param>
    /// <param name="tenantAccessor">Names the current tenant.</param>
    /// <param name="redactorProvider">The redactor provider used to redact sensitive values before logging.</param>
    public TelephonyHub(
        ILogger<TelephonyHub> logger,
        IStringLocalizer<TelephonyHub> stringLocalizer,
        IScopedWorkExecutor scopeExecutor,
        ITenantAccessor tenantAccessor,
        IRedactorProvider redactorProvider)
        : base(logger, stringLocalizer, scopeExecutor, tenantAccessor, redactorProvider)
    {
    }
}
