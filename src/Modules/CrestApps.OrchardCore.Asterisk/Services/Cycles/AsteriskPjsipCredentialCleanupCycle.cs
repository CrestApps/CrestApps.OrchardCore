using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Periodically reclaims expired browser SIP credentials so orphaned PJSIP realtime rows do not
/// accumulate in the Asterisk realtime store once their issued lifetime has elapsed. The sweep is a
/// no-op for tenants that have not issued any browser credentials.
/// </summary>
internal sealed class AsteriskPjsipCredentialCleanupCycle : IAsteriskPjsipCredentialCleanupCycle
{
    private readonly IAsteriskPjsipCredentialIssuer _credentialIssuer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskPjsipCredentialCleanupCycle"/> class.
    /// </summary>
    /// <param name="credentialIssuer">The credential issuer.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskPjsipCredentialCleanupCycle(
        IAsteriskPjsipCredentialIssuer credentialIssuer,
        ILogger<AsteriskPjsipCredentialCleanupCycle> logger)
    {
        _credentialIssuer = credentialIssuer;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _credentialIssuer.CleanupExpiredAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning up expired Asterisk browser SIP credentials.");
        }
    }
}
