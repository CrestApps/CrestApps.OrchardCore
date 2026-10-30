using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Scheduling.BackgroundTasks;

/// <summary>
/// Ends appointment holds that ran out, every minute.
/// </summary>
[BackgroundTask(
    Title = "Appointment holds",
    Schedule = "* * * * *",
    Description = "Ends appointment holds that ran out, so their time can be booked again.",
    LockTimeout = 1_000,
    LockExpiration = 120_000)]
public sealed class AppointmentHoldsBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return serviceProvider.GetRequiredService<IAppointmentService>().ExpireHoldsAsync();
    }
}
