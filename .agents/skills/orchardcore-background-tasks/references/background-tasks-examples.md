# Orchard Core Background Tasks Examples

## Example 1: Content Cleanup Task

A background task that removes unpublished draft content older than 30 days:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

[BackgroundTask(
    Schedule = "0 2 * * *",
    Description = "Removes draft content items older than 30 days.")]
public sealed class DraftCleanupTask : IBackgroundTask
{
    private readonly ILogger<DraftCleanupTask> _logger;

    public DraftCleanupTask(ILogger<DraftCleanupTask> logger)
    {
        _logger = logger;
    }

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var contentManager = serviceProvider.GetRequiredService<IContentManager>();
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var oldDrafts = await session
            .Query<ContentItem, ContentItemIndex>(x =>
                x.Latest && !x.Published && x.ModifiedUtc < cutoffDate)
            .ListAsync();

        var count = 0;
        foreach (var draft in oldDrafts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await contentManager.RemoveAsync(draft);
            count++;
        }

        _logger.LogInformation("Draft cleanup completed. Removed {Count} old drafts.", count);
    }
}
```

## Example 2: Registering the Task

```csharp
using OrchardCore.BackgroundTasks;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddBackgroundTask<DraftCleanupTask>();
    }
}
```
