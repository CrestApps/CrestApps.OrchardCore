using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Records AI completion usage metrics and delegates session-level token aggregation
/// to <see cref="AIChatSessionEventService"/>.
/// </summary>
public sealed class AICompletionUsageService : IAICompletionUsageObserver
{
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIChatSessionEventService _chatSessionEventService;
    private readonly GeneralAIOptions _generalAIOptions;
    private readonly YesSqlStoreOptions _yesSqlStoreOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AICompletionUsageService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used for persistence.</param>
    /// <param name="clock">The clock for UTC timestamps.</param>
    /// <param name="httpContextAccessor">The accessor for the current HTTP context.</param>
    /// <param name="chatSessionEventService">The service for recording session-level token usage.</param>
    /// <param name="generalAIOptions">The general AI options containing tracking configuration.</param>
    public AICompletionUsageService(
        ISession session,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        AIChatSessionEventService chatSessionEventService,
        IOptions<GeneralAIOptions> generalAIOptions,
        IOptions<YesSqlStoreOptions> yesSqlStoreOptions)
    {
        _session = session;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _chatSessionEventService = chatSessionEventService;
        _generalAIOptions = generalAIOptions.Value;
        _yesSqlStoreOptions = yesSqlStoreOptions.Value;
    }

    /// <summary>
    /// Records a completion usage entry and updates session-level token counts when applicable.
    /// </summary>
    /// <param name="record">The usage record to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task UsageRecordedAsync(AICompletionUsageRecord record, CancellationToken cancellationToken = default)
    {
        if (!_generalAIOptions.EnableAIUsageTracking)
        {
            return;
        }

        record.CreatedUtc = _clock.UtcNow;

        if (string.IsNullOrEmpty(record.UserName))
        {
            record.UserName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }

        await _session.SaveAsync(record, collection: _yesSqlStoreOptions.AICollectionName, cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(record.SessionId) &&
            (record.InputTokenCount > 0 || record.OutputTokenCount > 0))
        {
            await _chatSessionEventService.RecordCompletionUsageAsync(record.SessionId, record.InputTokenCount, record.OutputTokenCount);
        }
    }

    /// <summary>
    /// Retrieves completion usage records within the specified date range, ordered by most recent first.
    /// </summary>
    /// <param name="startDateUtc">The inclusive start date, or <c>null</c> for no lower bound.</param>
    /// <param name="endDateUtc">The inclusive end date, or <c>null</c> for no upper bound.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task<IReadOnlyList<AICompletionUsageRecord>> GetAsync(
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AICompletionUsageRecord, AICompletionUsageIndex>(collection: _yesSqlStoreOptions.AICollectionName);

        if (startDateUtc.HasValue)
        {
            var start = startDateUtc.Value.Date;
            query = query.Where(x => x.CreatedUtc >= start);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedUtc < endExclusive);
        }

        var records = await query.ListAsync(cancellationToken);

        return records.OrderByDescending(x => x.CreatedUtc).ToList();
    }
}
