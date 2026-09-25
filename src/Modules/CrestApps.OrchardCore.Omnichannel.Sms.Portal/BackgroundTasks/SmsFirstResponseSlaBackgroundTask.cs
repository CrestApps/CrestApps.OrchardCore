using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.BackgroundTasks;

/// <summary>
/// Announces conversations that have missed their first-response target. Cron cannot express a cadence shorter than
/// a minute, and a customer waiting on a five-minute target should not learn about it up to a minute late, so each
/// run makes one pass and holds an in-process deadline for the next — the soonest target still ahead, and never more
/// than thirty seconds out — instead of looping inside its run and holding the tenant's background loop.
/// </summary>
[BackgroundTask(
    Title = "SMS First Response SLA",
    Schedule = "* * * * *",
    Description = "Escalates SMS conversations that missed their first-response target.",
    LockTimeout = 3_000,
    LockExpiration = 55_000)]
public sealed class SmsFirstResponseSlaBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var sweep = serviceProvider.GetRequiredService<SmsFirstResponseSlaSweep>();
        var logger = serviceProvider.GetRequiredService<ILogger<SmsFirstResponseSlaBackgroundTask>>();

        try
        {
            await sweep.RunAndArmAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The next run tries again; a failed pass must not take the background loop down with it.
            logger.LogError(ex, "An SMS first-response escalation pass failed.");
        }
    }
}
