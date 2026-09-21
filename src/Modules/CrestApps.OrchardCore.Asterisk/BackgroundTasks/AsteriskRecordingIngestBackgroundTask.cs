using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using CrestApps.Core.Telephony.Asterisk;
using CrestApps.Core.Telephony.Asterisk.Services;
using CrestApps.Core.Telephony.Asterisk.Models;
using CrestApps.Core.Telephony.Asterisk.Data.YesSql.Indexes;
using CrestApps.Core.Telephony.Asterisk.Data.YesSql.Migrations;

namespace CrestApps.OrchardCore.Asterisk.BackgroundTasks;

/// <summary>
/// Periodically ingests completed conversation recordings from Asterisk into the encrypted media store. It is
/// the durable failed-upload recovery mechanism behind recording governance: a recording whose bytes were not
/// yet readable when it was stopped, or whose first ingest attempt failed, is retried here with back-off until
/// it is stored or dead-lettered. The sweep is a no-op for tenants with no pending recording ingest jobs.
/// </summary>
[BackgroundTask(
    Title = "Asterisk Recording Ingest",
    Schedule = "* * * * *",
    Description = "Securely ingests completed Asterisk recordings into the encrypted media store with retry and dead-lettering.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class AsteriskRecordingIngestBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAsteriskRecordingIngestCycle>().RunAsync(cancellationToken);
}
