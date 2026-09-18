using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using Microsoft.Extensions.DependencyInjection;
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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ISmsFirstResponseSlaCycle>().RunAsync(cancellationToken);
}
