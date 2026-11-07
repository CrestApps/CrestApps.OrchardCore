using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// Reads chat session events from a list, narrowed the way the YesSql reader narrows them, and remembers the last
/// query so a test can see the filters reached it.
/// </summary>
internal sealed class InMemoryAIChatSessionEventReader : IAIChatSessionEventReader
{
    public List<AIChatSessionEvent> Events { get; } = [];

    public AIChatSessionEventQuery LastQuery { get; private set; }

    public Task<IReadOnlyList<AIChatSessionEvent>> ListAsync(AIChatSessionEventQuery query, CancellationToken cancellationToken = default)
    {
        LastQuery = query;

        IReadOnlyList<AIChatSessionEvent> events = Events
            .Where(evt => string.IsNullOrEmpty(query.ProfileId) || evt.ProfileId == query.ProfileId)
            .Where(evt => query.FromUtc is null || evt.SessionStartedUtc >= query.FromUtc)
            .Where(evt => query.ToUtc is null || evt.SessionStartedUtc <= query.ToUtc)
            .OrderByDescending(evt => evt.SessionStartedUtc)
            .ToList();

        return Task.FromResult(events);
    }
}
