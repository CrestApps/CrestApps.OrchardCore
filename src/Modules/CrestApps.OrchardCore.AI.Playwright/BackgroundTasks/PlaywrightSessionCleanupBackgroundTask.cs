using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.AI.Playwright.BackgroundTasks;

[BackgroundTask(
    Title = "Playwright Session Cleanup",
    Schedule = "*/5 * * * *",
    Description = "Closes inactive Playwright browser sessions after their inactivity timeout elapses.",
    LockTimeout = 5_000,
    LockExpiration = 300_000)]
public sealed class PlaywrightSessionCleanupBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<PlaywrightSessionCleanupBackgroundTask>>();
        var sessionManager = serviceProvider.GetRequiredService<IPlaywrightSessionManager>();

        logger.LogDebug("Running Playwright session cleanup.");
        await sessionManager.CloseInactiveSessionsAsync(cancellationToken);
    }
}
