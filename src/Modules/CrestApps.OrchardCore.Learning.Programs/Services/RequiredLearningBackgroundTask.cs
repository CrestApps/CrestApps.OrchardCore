using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Services;

/// <summary>
/// Every hour: sends the reminders and escalations that are due, and runs each enabled rule that has not run for a
/// day, which picks up people who joined or left its audience.
/// </summary>
[BackgroundTask(
    Title = "Required learning upkeep",
    Schedule = "15 * * * *",
    Description = "Sends required learning reminders and escalations, and runs each rule once a day.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
internal sealed class RequiredLearningBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _runEvery = TimeSpan.FromHours(20);

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var service = serviceProvider.GetRequiredService<IRequiredLearningService>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<RequiredLearningBackgroundTask>>();

        foreach (var requirement in await service.ListAsync(cancellationToken))
        {
            if (!requirement.Enabled || requirement.LastRunUtc is { } last && clock.UtcNow - last < _runEvery)
            {
                continue;
            }

            try
            {
                await service.RunAsync(requirement.ItemId, "system", cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "A required learning rule could not run; it runs again in an hour.");
            }
        }

        await service.SendRemindersAsync(cancellationToken);
    }
}
