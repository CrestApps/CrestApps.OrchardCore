using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

[BackgroundTask(
    Title = "Omnichannel Automated Activities Processor",
    Schedule = "*/5 * * * *",
    Description = "Processes omnichannel activities.",
    LockTimeout = 5_000,
    LockExpiration = 90_000)]
public sealed class AutomatedActivitiesProcessorBackgroundTask : IBackgroundTask
{
    private const int _batchSize = 100;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // Get all activities that are scheduled to run and process them.
        var session = serviceProvider.GetRequiredService<ISession>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var processors = serviceProvider.GetService<IEnumerable<IOmnichannelProcessor>>()
            .ToDictionary(x => x.Channel, StringComparer.OrdinalIgnoreCase);

        var logger = serviceProvider.GetRequiredService<ILogger<AutomatedActivitiesProcessorBackgroundTask>>();

        if (processors.Count == 0)
        {
            logger.LogWarning("No omnichannel processors were found. Make sure at least one processor is registered.");

            return;
        }

        var now = clock.UtcNow;
        long documentId = 0;

        var iterationCount = 0;

        while (true)
        {
            var activities = await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(x =>
                    x.Status == ActivityStatus.NotStated &&
                    x.InteractionType == ActivityInteractionType.Automated &&
                    x.ScheduledUtc <= now &&
                    x.Channel.IsIn(processors.Keys) &&
                    x.DocumentId > documentId, collection: OmnichannelConstants.CollectionName)
                .OrderBy(x => x.DocumentId)
                .Skip(iterationCount++ * _batchSize)
                .Take(_batchSize)
                .ListAsync(cancellationToken);

            if (!activities.Any())
            {
                break;
            }

            foreach (var activity in activities)
            {
                documentId = Math.Max(documentId, activity.Id);

                try
                {
                    var processor = processors[activity.Channel];

                    await processor.StartAsync(activity, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the activity with id '{ActivityId}'", activity.ActivityId);
                }

                documentId = activity.Id;

                await session.SaveAsync(activity, false, collection: OmnichannelConstants.CollectionName, cancellationToken);
            }

            await session.FlushAsync(cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }
}
