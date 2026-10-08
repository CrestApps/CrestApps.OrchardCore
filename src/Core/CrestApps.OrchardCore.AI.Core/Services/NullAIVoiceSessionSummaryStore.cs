using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The store a tenant has while the usage analytics feature, which owns the table, is off: it keeps nothing.
/// </summary>
public sealed class NullAIVoiceSessionSummaryStore : IAIVoiceSessionSummaryStore
{
    /// <inheritdoc/>
    public Task<AIVoiceSessionSummary> FindByActivityAsync(string activityId, CancellationToken cancellationToken = default)
        => Task.FromResult<AIVoiceSessionSummary>(null);

    /// <inheritdoc/>
    public Task SaveAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task DeleteAsync(AIVoiceSessionSummary summary, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIVoiceSessionSummaryIndex>> GetAsync(DateTime? startDateUtc, DateTime? endDateUtc, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AIVoiceSessionSummaryIndex>>([]);
}
