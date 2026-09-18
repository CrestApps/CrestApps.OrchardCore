using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Periodically returns routed (push-assigned) SMS conversations that the assigned agent has not picked up
/// within the grace window to their queue's shared pool, so a message never stalls in one inbox. The sweep is a
/// no-op when there are no unpicked routed conversations.
/// </summary>
public sealed class SmsRoutedReassignmentCycle : ISmsRoutedReassignmentCycle
{
    private readonly ISmsRoutedReassignmentService _reassignmentService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsRoutedReassignmentCycle"/> class.
    /// </summary>
    /// <param name="reassignmentService">The reassignment service.</param>
    /// <param name="logger">The logger.</param>
    public SmsRoutedReassignmentCycle(
        ISmsRoutedReassignmentService reassignmentService,
        ILogger<SmsRoutedReassignmentCycle> logger)
    {
        _reassignmentService = reassignmentService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _reassignmentService.ReassignStaleAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while reassigning stale routed SMS conversations.");
        }
    }
}
