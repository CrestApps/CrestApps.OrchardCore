using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The default implementation of <see cref="ISmsBroadcastManager"/>.
/// </summary>
public sealed class SmsBroadcastManager : CatalogManager<SmsBroadcast>, ISmsBroadcastManager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsBroadcastManager"/> class.
    /// </summary>
    /// <param name="store">The underlying broadcast store.</param>
    /// <param name="handlers">The catalog entry handlers for broadcasts.</param>
    /// <param name="logger">The logger instance.</param>
    public SmsBroadcastManager(
        ISmsBroadcastStore store,
        IEnumerable<ICatalogEntryHandler<SmsBroadcast>> handlers,
        ILogger<CatalogManager<SmsBroadcast>> logger)
        : base(store, handlers, logger)
    {
    }
}
