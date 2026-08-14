using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.Extensions.DependencyInjection;
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
        var chunkFileUploadService = serviceProvider.GetRequiredService<IContentTransferChunkFileUploadService>();
        chunkFileUploadService.PurgeTempDirectory();

        return Task.CompletedTask;
    }
}
