using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telnyx.BackgroundTasks;

/// <summary>
/// Periodically ingests completed Telnyx recordings into the encrypted media store. It is the durable
/// failed-upload recovery mechanism behind recording governance: a recording whose bytes were not yet
/// downloadable when the saved webhook arrived, or whose first ingest attempt failed, is retried here with
/// back-off until it is stored or dead-lettered. The sweep is a no-op for tenants with no pending recording
/// ingest jobs.
/// </summary>
[BackgroundTask(
    Title = "Telnyx Recording Ingest",
    Schedule = "* * * * *",
    Description = "Securely ingests completed Telnyx recordings into the encrypted media store with retry and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class TelnyxRecordingIngestBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var ingestService = serviceProvider.GetRequiredService<ITelnyxRecordingIngestService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TelnyxRecordingIngestBackgroundTask>>();

        try
        {
            var ingested = await ingestService.ProcessDueAsync(cancellationToken);

            if (ingested > 0 && logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Ingested {Count} Telnyx recording(s) into the media store.", ingested);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while ingesting Telnyx recordings.");
        }
    }
}
