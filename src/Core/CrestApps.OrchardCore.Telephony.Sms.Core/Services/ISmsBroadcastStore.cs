using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The persistence contract for <see cref="SmsBroadcast"/>.
/// </summary>
public interface ISmsBroadcastStore : ICatalog<SmsBroadcast>
{
    /// <summary>
    /// Lists the broadcasts in the specified status, oldest first.
    /// </summary>
    /// <param name="status">The status to match.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching broadcasts.</returns>
    Task<IReadOnlyCollection<SmsBroadcast>> GetByStatusAsync(SmsBroadcastStatus status, CancellationToken cancellationToken = default);
}
