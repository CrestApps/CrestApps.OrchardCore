using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.MailingLists.BackgroundTasks;

/// <summary>
/// Sends the member changes of the provider outbox that are due to the lists' providers, a batch at a time through the
/// background work pacer.
/// </summary>
[BackgroundTask(
    Title = "Send mailing list changes to providers",
    Schedule = "* * * * *",
    Description = "Sends member changes to the lists' outside providers, tries again the ones a provider did not take, marks members not synced after the last attempt, and keeps the changes of a provider that is off or not set up until it is back.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class MailingListSyncBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The most batches one run sends.
    /// </summary>
    public const int MaxBatchesPerRun = 10;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var processor = serviceProvider.GetRequiredService<MailingListSyncProcessor>();
            var pacing = serviceProvider.GetService<IOptions<BackgroundWorkPacingOptions>>()?.Value;
            var claimed = 0;

            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                var result = await BackgroundWorkPacer.RunBatchAsync(processor.RunBatchAsync, pacing, cancellationToken);

                claimed += result.Claimed;
                run.AddItems(result.Sent, "sent");
                run.AddItems(result.Retried, "retried");
                run.AddItems(result.Abandoned, "abandoned");
                run.AddItems(result.Waiting, "waiting");

                if (result.Claimed < processor.BatchSize)
                {
                    break;
                }
            }

            if (claimed == 0)
            {
                run.Skip();
            }
        });
}
