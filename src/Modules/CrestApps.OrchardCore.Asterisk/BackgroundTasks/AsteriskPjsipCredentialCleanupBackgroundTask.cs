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
/// Periodically reclaims expired browser SIP credentials so orphaned PJSIP realtime rows do not
/// accumulate in the Asterisk realtime store once their issued lifetime has elapsed. The sweep is a
/// no-op for tenants that have not issued any browser credentials.
/// </summary>
[BackgroundTask(
    Title = "Asterisk Browser SIP Credential Cleanup",
    Schedule = "*/5 * * * *",
    Description = "Reclaims expired browser SIP credentials from the Asterisk realtime store.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class AsteriskPjsipCredentialCleanupBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAsteriskPjsipCredentialCleanupCycle>().RunAsync(cancellationToken);
}
