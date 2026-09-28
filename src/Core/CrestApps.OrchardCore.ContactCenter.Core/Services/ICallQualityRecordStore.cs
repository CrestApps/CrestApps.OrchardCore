using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Stores the quality of each call leg.
/// </summary>
public interface ICallQualityRecordStore : ICatalog<CallQualityRecord>
{
    /// <summary>
    /// Finds the record of one source's measurement of one leg.
    /// </summary>
    /// <param name="recordKey">The key built by <see cref="CallQualityRecord.BuildRecordKey"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The record, or <see langword="null"/> when there is none.</returns>
    Task<CallQualityRecord> FindByRecordKeyAsync(string recordKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the records of legs measured within a period, oldest first.
    /// </summary>
    /// <param name="fromUtc">The start of the period, inclusive.</param>
    /// <param name="toUtc">The end of the period, exclusive.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyCollection<CallQualityRecord>> GetObservedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an agent's most recent records, newest first.
    /// </summary>
    /// <param name="agentId">The agent.</param>
    /// <param name="sinceUtc">How far back to look.</param>
    /// <param name="count">The largest number of records to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyCollection<CallQualityRecord>> GetRecentForAgentAsync(string agentId, DateTime sinceUtc, int count, CancellationToken cancellationToken = default);
}
