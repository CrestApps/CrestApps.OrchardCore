using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Documents;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Indexes, once, the routing decisions and dial suppressions written before the dialer decision log existed, so the
/// decision log report covers them too.
/// </summary>
/// <remarks>
/// It runs here rather than in the migration that creates the log's tables, because saving every past decision during
/// activation would hold up a tenant with a long event history. Each run indexes a bounded number of pages and
/// remembers where it stopped, so it resumes after a restart and stops for good once it has reached the end.
/// </remarks>
[BackgroundTask(
    Title = "Contact Center Dialer Decision Index Backfill",
    Schedule = "* * * * *",
    Description = "Indexes the routing decisions and dial suppressions recorded before the dialer decision log existed.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class DialerDecisionIndexBackfillBackgroundTask : IBackgroundTask
{
    private const int PagesPerRun = 25;

    private bool _completed;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (_completed)
        {
            return;
        }

        var stateManager = serviceProvider.GetRequiredService<IDocumentManager<ContactCenterBackfillState>>();
        var state = await stateManager.GetOrCreateMutableAsync();
        var store = serviceProvider.GetRequiredService<IStore>();
        var logger = serviceProvider.GetRequiredService<ILogger<DialerDecisionIndexBackfillBackgroundTask>>();
        var finished = true;
        var indexed = 0;

        try
        {
            foreach (var source in DialerDecisionIndexBackfill.Sources)
            {
                if (state.Completed.Contains(source.Name))
                {
                    continue;
                }

                var result = await DialerDecisionIndexBackfill.RunAsync(
                    store,
                    source,
                    state.Cursors.GetValueOrDefault(source.Name),
                    PagesPerRun,
                    cancellationToken);

                indexed += result.Indexed;

                if (result.Finished)
                {
                    state.Completed.Add(source.Name);
                    state.Cursors.Remove(source.Name);
                }
                else
                {
                    state.Cursors[source.Name] = result.Cursor;
                    finished = false;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A decision missing from a report is not a reason to disturb the tenant; the next run picks up from the
            // last page that was saved.
            logger.LogWarning(ex, "Could not index the dialer decisions recorded before the dialer decision log existed.");
            finished = false;
        }

        if (indexed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Indexed {DecisionCount} dialer decisions recorded before the dialer decision log existed.",
                indexed);
        }

        await stateManager.UpdateAsync(state);

        _completed = finished;
    }
}
