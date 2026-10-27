using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Keeps conversation evaluations in the AI collection, beside the sessions they score.
/// </summary>
public sealed class YesSqlAIConversationEvaluationStore : IAIConversationEvaluationStore
{
    private readonly ISession _session;
    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance of the <see cref="YesSqlAIConversationEvaluationStore"/> class.
    /// </summary>
    /// <param name="session">The document session.</param>
    /// <param name="options">The store options that name the AI collection.</param>
    public YesSqlAIConversationEvaluationStore(ISession session, IOptions<YesSqlStoreOptions> options)
    {
        _session = session;
        _collectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public async Task SaveAsync(AIConversationEvaluation evaluation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        await _session.SaveAsync(evaluation, false, _collectionName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AIConversationEvaluation> FindLatestBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        return await _session
            .Query<AIConversationEvaluation, AIConversationEvaluationIndex>(index => index.SessionId == sessionId, _collectionName)
            .OrderByDescending(index => index.EvaluatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIConversationEvaluationIndex>> GetAsync(string profileId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var query = _session.QueryIndex<AIConversationEvaluationIndex>(
            index => index.EvaluatedUtc >= fromUtc && index.EvaluatedUtc < toUtc,
            _collectionName);

        if (!string.IsNullOrEmpty(profileId))
        {
            query = query.Where(index => index.ProfileId == profileId);
        }

        var rows = await query.ListAsync(cancellationToken);

        return rows.ToList();
    }
}
