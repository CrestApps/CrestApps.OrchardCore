using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var credentialIssuer = serviceProvider.GetRequiredService<IAsteriskPjsipCredentialIssuer>();
        var logger = serviceProvider.GetRequiredService<ILogger<AsteriskPjsipCredentialCleanupBackgroundTask>>();

        try
        {
            await credentialIssuer.CleanupExpiredAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while cleaning up expired Asterisk browser SIP credentials.");
        }
    }
}
