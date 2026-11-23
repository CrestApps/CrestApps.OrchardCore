using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// Tries again the emails of the templated email outbox that the email service didn't take, a batch at a time through
/// the background work pacer, and gives each up after its last attempt.
/// </summary>
[BackgroundTask(
    Title = "Send templated e-mails",
    Schedule = "* * * * *",
    Description = "Tries again the e-mails the e-mail service didn't take, and gives up after six tries.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class TemplatedEmailOutboxBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// How many emails one batch tries.
    /// </summary>
    public const int BatchSize = 50;

    /// <summary>
    /// The most batches one run tries.
    /// </summary>
    public const int MaxBatchesPerRun = 10;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run =>
        {
            var outbox = serviceProvider.GetRequiredService<TemplatedEmailOutbox>();
            var pacing = serviceProvider.GetService<IOptions<BackgroundWorkPacingOptions>>()?.Value;
            var claimed = 0;

            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                var result = await BackgroundWorkPacer.RunBatchAsync(token => outbox.SendDueAsync(BatchSize, token), pacing, cancellationToken);

                claimed += result.Claimed;
                run.AddItems(result.Sent, "sent");
                run.AddItems(result.Retried, "retried");
                run.AddItems(result.Abandoned, "abandoned");

                if (result.Claimed < BatchSize)
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
