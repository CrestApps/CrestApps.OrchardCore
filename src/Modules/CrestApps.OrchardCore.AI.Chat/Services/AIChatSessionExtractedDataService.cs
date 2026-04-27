using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Provides AI chat session extracted data services.
/// </summary>
public sealed class AIChatSessionExtractedDataService : IAIChatSessionExtractedDataRecorder
{
    private readonly ISession _session;
    private readonly YesSqlStoreOptions _yesSqlStoreOptions;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatSessionExtractedDataService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    public AIChatSessionExtractedDataService(
        ISession session,
        IOptions<YesSqlStoreOptions> yesSqlStoreOptions,
        IClock clock)
    {
        _session = session;
        _yesSqlStoreOptions = yesSqlStoreOptions.Value;
        _clock = clock;
    }

    /// <summary>
    /// Asynchronously performs the record extracted data operation.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RecordExtractedDataAsync(
        AIProfile profile,
        AIChatSession session,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindBySessionIdAsync(session.SessionId);

        if (session.ExtractedData.Count == 0)
        {
            if (existing is not null)
            {
                _session.Delete(existing);
            }

            return;
        }

        var record = existing ?? new AIChatSessionExtractedDataRecord
        {
            ItemId = session.SessionId,
            SessionId = session.SessionId,
        };

        record.ProfileId = profile.ItemId;
        record.SessionStartedUtc = session.CreatedUtc;
        record.SessionEndedUtc = session.ClosedAtUtc;
        record.UpdatedUtc = _clock.UtcNow;
        record.Values = session.ExtractedData
            .Where(pair => pair.Value.Values.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Values.ToList(),
                StringComparer.OrdinalIgnoreCase);

        await _session.SaveAsync(record, collection: _yesSqlStoreOptions.AICollectionName, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Retrieves the async.
    /// </summary>
    /// <param name="profileId">The profile id.</param>
    /// <param name="startDateUtc">The start date utc.</param>
    /// <param name="endDateUtc">The end date utc.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<AIChatSessionExtractedDataRecord>> GetAsync(
        string profileId,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(
            index => index.ProfileId == profileId,
            collection: _yesSqlStoreOptions.AICollectionName);

        if (startDateUtc.HasValue)
        {
            var start = startDateUtc.Value.Date;
            query = query.Where(index => index.SessionStartedUtc >= start);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.Date.AddDays(1);
            query = query.Where(index => index.SessionStartedUtc < endExclusive);
        }

        var records = await query.ListAsync(cancellationToken);
        return records.OrderByDescending(record => record.SessionStartedUtc).ToList();
    }

    private async Task<AIChatSessionExtractedDataRecord> FindBySessionIdAsync(string sessionId) =>
        await _session.Query<AIChatSessionExtractedDataRecord, AIChatSessionExtractedDataIndex>(
            index => index.SessionId == sessionId,
            collection: _yesSqlStoreOptions.AICollectionName)
            .FirstOrDefaultAsync();
}
