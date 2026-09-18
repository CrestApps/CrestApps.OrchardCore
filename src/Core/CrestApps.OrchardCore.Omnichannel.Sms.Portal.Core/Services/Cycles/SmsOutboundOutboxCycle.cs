using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Re-attempts outbound SMS the provider refused. The send an agent makes is still tried inside their request so
/// the composer answers immediately, but a provider that is briefly unreachable no longer costs them the
/// message: it stays queued and this pass retries it on a widening schedule until it is accepted or the schedule
/// is exhausted.
/// </summary>
public sealed class SmsOutboundOutboxCycle : ISmsOutboundOutboxCycle
{
    private readonly ISmsOutboundOutbox _outbox;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsOutboundOutboxCycle"/> class.
    /// </summary>
    /// <param name="outbox">The outbox.</param>
    /// <param name="logger">The logger.</param>
    public SmsOutboundOutboxCycle(
        ISmsOutboundOutbox outbox,
        ILogger<SmsOutboundOutboxCycle> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _outbox.DispatchDueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrying queued outbound SMS messages.");
        }
    }
}
