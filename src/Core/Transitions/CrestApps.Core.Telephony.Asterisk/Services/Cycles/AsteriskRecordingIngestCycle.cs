using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Telephony.Asterisk.Services;

/// <summary>
/// Periodically ingests completed conversation recordings from Asterisk into the encrypted media store. It is
/// the durable failed-upload recovery mechanism behind recording governance: a recording whose bytes were not
/// yet readable when it was stopped, or whose first ingest attempt failed, is retried here with back-off until
/// it is stored or dead-lettered. The sweep is a no-op for tenants with no pending recording ingest jobs.
/// </summary>
public sealed class AsteriskRecordingIngestCycle : IAsteriskRecordingIngestCycle
{
    private readonly IAsteriskRecordingIngestService _ingestService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskRecordingIngestCycle"/> class.
    /// </summary>
    /// <param name="ingestService">The ingest service.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskRecordingIngestCycle(
        IAsteriskRecordingIngestService ingestService,
        ILogger<AsteriskRecordingIngestCycle> logger)
    {
        _ingestService = ingestService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var ingested = await _ingestService.ProcessDueAsync(cancellationToken);

            if (ingested > 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Ingested {Count} Asterisk recording(s) into the media store.", ingested);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while ingesting Asterisk recordings.");
        }
    }
}
