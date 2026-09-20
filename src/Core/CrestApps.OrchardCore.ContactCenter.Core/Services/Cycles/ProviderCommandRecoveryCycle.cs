using CrestApps.Core.ContactCenter;
using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Recovers due provider commands so ambiguous or interrupted provider operations are resumed through the
/// durable provider-command state machine.
/// </summary>
public sealed class ProviderCommandRecoveryCycle : IProviderCommandRecoveryCycle
{
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly IProviderCommandProcessor _processor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandRecoveryCycle"/> class.
    /// </summary>
    /// <param name="workManager">The work manager.</param>
    /// <param name="processor">The processor.</param>
    /// <param name="logger">The logger.</param>
    public ProviderCommandRecoveryCycle(
        IContactCenterFeatureWorkManager workManager,
        IProviderCommandProcessor processor,
        ILogger<ProviderCommandRecoveryCycle> logger)
    {
        _workManager = workManager;
        _processor = processor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var workLease = _workManager.TryEnter(ContactCenterCapabilities.Voice);

        if (workLease is null)
        {
            return;
        }


        try
        {
            await _processor.RecoverDueAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while recovering Contact Center provider commands for feature {FeatureId}.",
                ContactCenterFeatures.Voice);
        }
    }
}
