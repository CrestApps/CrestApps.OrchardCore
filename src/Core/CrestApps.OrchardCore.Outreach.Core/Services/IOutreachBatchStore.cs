using CrestApps.Core.Services;
using CrestApps.OrchardCore.Outreach.Core.Models;

namespace CrestApps.OrchardCore.Outreach.Core.Services;

/// <summary>
/// Stores outreach batches.
/// </summary>
public interface IOutreachBatchStore : ICatalog<OutreachBatch>
{
    /// <summary>
    /// Lists the batches that can be claimed at a time, oldest first: pending ones whose time has come and claimed ones
    /// whose lease expired.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <param name="maxCount">The most batches to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due batches.</returns>
    Task<IReadOnlyCollection<OutreachBatch>> GetDueAsync(DateTime nowUtc, int maxCount, CancellationToken cancellationToken = default);
}
