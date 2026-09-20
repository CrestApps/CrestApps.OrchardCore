using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Promotes due callbacks into outbound activities so the dialer or an agent can handle them.
/// </summary>
public sealed class CallbackDispatchCycle : ICallbackDispatchCycle
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ICallbackService _callbackService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDispatchCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="callbackService">The callback service.</param>
    /// <param name="logger">The logger.</param>
    public CallbackDispatchCycle(
        IContactCenterFeatureWorkManager workManager,
        ICallbackService callbackService,
        ILogger<CallbackDispatchCycle> logger)
    {
        _workManager = workManager;
        _callbackService = callbackService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Dialer);

        if (workLease is null)
        {
            return;
        }


        try
        {
            await _callbackService.PromoteDueAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while promoting due callbacks.");
        }
    }
}
