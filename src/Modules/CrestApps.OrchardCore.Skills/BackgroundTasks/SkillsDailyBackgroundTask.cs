using CrestApps.OrchardCore.Skills.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Skills.BackgroundTasks;

/// <summary>
/// Runs the daily skills check once per day in the site's time zone: certificate reminders and expiries, and
/// assignments that start or end that day. It wakes every hour and skips a day it already ran.
/// </summary>
[BackgroundTask(
    Title = "Skills and certificates daily check",
    Schedule = "5 * * * *",
    Description = "Raises certificate reminders, marks certificates past their expiry date as expired, and updates the people whose skills start or end today.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class SkillsDailyBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var clock = serviceProvider.GetRequiredService<IClock>();
        var site = await serviceProvider.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
        var zone = clock.GetTimeZone(site.TimeZoneId);
        var today = DateOnly.FromDateTime(clock.ConvertToTimeZone(clock.UtcNow, zone).DateTime);
        var checkpoints = serviceProvider.GetRequiredService<SkillsDailyCheckpointStore>();

        if (await checkpoints.GetLastRunAsync(cancellationToken) >= today)
        {
            return;
        }

        await serviceProvider.GetRequiredService<SkillsDailyCheck>().RunAsync(today, cancellationToken);
        await checkpoints.SetLastRunAsync(today, cancellationToken);
    }
}
