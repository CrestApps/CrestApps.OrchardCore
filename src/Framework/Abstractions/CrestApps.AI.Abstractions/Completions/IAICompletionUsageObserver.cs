using CrestApps.AI.Models;

namespace CrestApps.AI.Completions;

public interface IAICompletionUsageObserver
{
    Task UsageRecordedAsync(AICompletionUsageRecord record, CancellationToken cancellationToken = default);
}
