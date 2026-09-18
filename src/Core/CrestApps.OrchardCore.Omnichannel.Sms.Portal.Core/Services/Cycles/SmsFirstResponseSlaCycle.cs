using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Announces conversations that have missed their first-response target. Cron cannot express a cadence shorter
/// than a minute, so the pass loops inside its minute at the configured interval; a customer waiting on a
/// five-minute target should not learn about it up to a minute late.
/// </summary>
public sealed class SmsFirstResponseSlaCycle : ISmsFirstResponseSlaCycle
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(50);

    private readonly ISmsFirstResponseSlaService _service;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsFirstResponseSlaCycle"/> class.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="logger">The logger.</param>
    public SmsFirstResponseSlaCycle(
        ISmsFirstResponseSlaService service,
        ILogger<SmsFirstResponseSlaCycle> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _budget;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _service.EscalateOverdueAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // One bad pass must not stop the rest of the minute: the next customer waiting is unrelated to
                // whatever this one tripped over.
                _logger.LogError(ex, "An SMS first-response escalation pass failed.");
            }

            if (DateTime.UtcNow + _interval >= deadline)
            {
                break;
            }

            await Task.Delay(_interval, cancellationToken);
        }
    }
}
