using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Completions;

public interface IAICompletionUsageObserver
{
    Task UsageRecordedAsync(AICompletionUsageRecord record, CancellationToken cancellationToken = default);
}
