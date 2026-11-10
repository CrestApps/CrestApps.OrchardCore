using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.ContentTransfer.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;

[BackgroundTask(
    Title = "Content Transfer Upload Cleanup",
    Schedule = "0 * * * *",
    Description = "Removes abandoned temporary files left over from chunked import uploads.",
    LockTimeout = 3_000,
    LockExpiration = 30_000)]
public sealed class ContentTransferUploadCleanupBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(run =>
        {
            var chunkFileUploadService = serviceProvider.GetRequiredService<IContentTransferChunkFileUploadService>();
            var deleted = chunkFileUploadService.PurgeTempDirectory();

            if (deleted == 0)
            {
                run.Skip();

                return Task.CompletedTask;
            }

            run.AddItems(deleted, "deleted");

            var logger = serviceProvider.GetRequiredService<ILogger<ContentTransferUploadCleanupBackgroundTask>>();

            if (logger.IsEnabled(LogLevel.Information))
            {
                ContentTransferLog.UploadTempFilesPurged(logger, deleted);
            }

            return Task.CompletedTask;
        });
    }
}
