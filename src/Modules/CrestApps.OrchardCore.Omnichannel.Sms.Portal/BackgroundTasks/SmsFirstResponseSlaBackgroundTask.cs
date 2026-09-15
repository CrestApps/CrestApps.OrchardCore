using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;

/// <summary>
/// Announces conversations that have missed their first-response target. Cron cannot express a cadence shorter
/// than a minute, so the pass loops inside its minute at the configured interval; a customer waiting on a
/// five-minute target should not learn about it up to a minute late.
/// </summary>
[BackgroundTask(
    Title = "SMS First Response SLA",
    Schedule = "* * * * *",
    Description = "Escalates SMS conversations that missed their first-response target.",
    LockTimeout = 3_000,
    LockExpiration = 55_000)]
public sealed class SmsFirstResponseSlaBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(50);

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<ISmsFirstResponseSlaService>();
        var logger = serviceProvider.GetRequiredService<ILogger<SmsFirstResponseSlaBackgroundTask>>();
        var deadline = DateTime.UtcNow + _budget;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await service.EscalateOverdueAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // One bad pass must not stop the rest of the minute: the next customer waiting is unrelated to
                // whatever this one tripped over.
                logger.LogError(ex, "An SMS first-response escalation pass failed.");
            }

            if (DateTime.UtcNow + _interval >= deadline)
            {
                break;
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }
}
