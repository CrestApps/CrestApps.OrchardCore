using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps voice session summaries in the AI collection, beside the completion usage records.
/// </summary>
public sealed class YesSqlAIVoiceSessionSummaryStore : IAIVoiceSessionSummaryStore
{
    private readonly ISession _session;
    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlAIVoiceSessionSummaryStore"/> class.
    /// </summary>
    /// <param name="session">The document session.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public YesSqlAIVoiceSessionSummaryStore(ISession session, IOptions<YesSqlStoreOptions> options)
    {
        _session = session;
        _collectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task<AIVoiceSessionSummary> FindByActivityAsync(string activityId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(activityId))
        {
            return null;
        }

        return await _session
            .Query<AIVoiceSessionSummary, AIVoiceSessionSummaryIndex>(index => index.ActivityId == activityId, _collectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        await _session.SaveAsync(summary, false, _collectionName, cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        _session.Delete(summary, _collectionName);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIVoiceSessionSummaryIndex>> GetAsync(DateTime? startDateUtc, DateTime? endDateUtc, CancellationToken cancellationToken = default)
    {
        var query = _session.QueryIndex<AIVoiceSessionSummaryIndex>(_collectionName);

        // The same window the completion usage records are read with, so both halves of the report cover the
        // same days.
        if (startDateUtc.HasValue)
        {
            var start = startDateUtc.Value.Date;
            query = query.Where(index => index.CreatedUtc >= start);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.Date.AddDays(1);
            query = query.Where(index => index.CreatedUtc < endExclusive);
        }

        var rows = await query.ListAsync(cancellationToken);

        return rows.ToList();
    }
}
