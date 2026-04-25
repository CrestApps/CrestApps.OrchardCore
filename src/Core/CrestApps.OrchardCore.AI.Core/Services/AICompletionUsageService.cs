using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using YesSql;

using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class AICompletionUsageService : IAICompletionUsageObserver
{
    private readonly ISession _session;
    private readonly TimeProvider _timeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIChatSessionEventService _chatSessionEventService;
    private readonly GeneralAIOptions _generalAIOptions;

    public AICompletionUsageService(
        ISession session,
        TimeProvider timeProvider,
        IHttpContextAccessor httpContextAccessor,
        AIChatSessionEventService chatSessionEventService,
        IOptions<GeneralAIOptions> generalAIOptions)
    {
        _session = session;
        _timeProvider = timeProvider;
        _httpContextAccessor = httpContextAccessor;
        _chatSessionEventService = chatSessionEventService;
        _generalAIOptions = generalAIOptions.Value;
    }

    public async Task UsageRecordedAsync(AICompletionUsageRecord record, CancellationToken cancellationToken = default)
    {
        if (!_generalAIOptions.EnableAIUsageTracking)
        {
            return;
        }

        record.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (string.IsNullOrEmpty(record.UserName))
        {
            record.UserName = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }

        await _session.SaveAsync(record, collection: AIConstants.AICollectionName, cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(record.SessionId) &&
            (record.InputTokenCount > 0 || record.OutputTokenCount > 0))
        {
            await _chatSessionEventService.RecordCompletionUsageAsync(record.SessionId, record.InputTokenCount, record.OutputTokenCount);
        }
    }

    public async Task<IReadOnlyList<AICompletionUsageRecord>> GetAsync(
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AICompletionUsageRecord, AICompletionUsageIndex>(collection: AIConstants.AICollectionName);

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
