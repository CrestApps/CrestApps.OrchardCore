using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Documents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Indexes the dialer profile of the open dialer activities saved before the activity index carried it, so Manage
/// Activities can filter them by dialer profile.
/// </summary>
/// <remarks>
/// An activity is indexed again whenever it is saved, so only the open records nothing has touched since need this.
/// It runs here rather than in the migration that adds the column, because re-saving every open dialer record during
/// activation would hold up the tenant on a large list.
/// </remarks>
[BackgroundTask(
    Title = "Omnichannel Activity Dialer Profile Backfill",
    Schedule = "* * * * *",
    Description = "Indexes the dialer profile of open dialer activities saved before the index carried it.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class ActivityDialerProfileIndexBackfillBackgroundTask : IBackgroundTask
{
    internal const string BackfillName = "ActivityDialerProfileId";

    private const int PageSize = 200;
    private const int PagesPerRun = 25;

    private static readonly string[] _dialerSources =
    [
        ActivitySources.Dialer,
        ActivitySources.PreviewDial,
        ActivitySources.PowerDial,
        ActivitySources.ProgressiveDial,
        ActivitySources.PredictiveDial,
        ActivitySources.Callback,
    ];

    private static readonly ActivityStatus[] _openStatuses =
    [
        ActivityStatus.NotStated,
        ActivityStatus.Scheduled,
        ActivityStatus.Pending,
        ActivityStatus.AwaitingAgentResponse,
        ActivityStatus.Failed,
        ActivityStatus.Cancelled,
    ];

    private bool _completed;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<OmnichannelBackfillState>>();
        var state = await stateManager.GetOrCreateMutableAsync();

        if (state.Completed.Contains(BackfillName))
        {
            _completed = true;

            return;
        }

        var store = serviceProvider.GetRequiredService<IStore>();
        var cursor = state.Cursors.GetValueOrDefault(BackfillName);
        var reindexed = 0;
        var finished = false;

        for (var page = 0; page < PagesPerRun && !finished; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var session = store.CreateSession();

            var activities = (await session.Query<OmnichannelActivity, OmnichannelActivityIndex>(index =>
                    index.DialerProfileId == null &&
                    index.Source.IsIn(_dialerSources) &&
                    index.Status.IsIn(_openStatuses) &&
                    index.DocumentId > cursor,
                    collection: OmnichannelConstants.CollectionName)
                .OrderBy(index => index.DocumentId)
                .Take(PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (activities.Count == 0)
            {
                finished = true;

                break;
            }

            foreach (var activity in activities)
            {
                cursor = Math.Max(cursor, activity.Id);

                // Saving the activity unchanged writes its index row again, now with the dialer profile it records.
                if (!string.IsNullOrEmpty(activity.DialerProfileId))
                {
                    await session.SaveAsync(activity, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
                    reindexed++;
                }
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        if (reindexed > 0)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<ActivityDialerProfileIndexBackfillBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Indexed the dialer profile of {ActivityCount} open dialer activities saved before the index carried it.", reindexed);
            }
        }

        if (finished)
        {
            state.Completed.Add(BackfillName);
            state.Cursors.Remove(BackfillName);
            _completed = true;
        }
        else
        {
            state.Cursors[BackfillName] = cursor;
        }

        await stateManager.UpdateAsync(state);
    }
}
