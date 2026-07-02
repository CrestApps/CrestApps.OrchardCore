using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerService"/>. The service validates the
/// profile, ensures a voice provider can place outbound calls, and delegates pacing to the registered
/// <see cref="IDialerStrategy"/> for the profile's mode. Agent-driven modes (Manual and Preview) and
/// unsupported modes (such as the blocked Predictive mode) do not run an automated cycle.
/// </summary>
public sealed class DialerService : IDialerService
{
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IDialerStrategyResolver _strategyResolver;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerService"/> class.
    /// </summary>
    /// <param name="voiceCallRouter">The voice call router used to confirm outbound routing is available.</param>
    /// <param name="strategyResolver">The resolver that maps a dialing mode to its strategy.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerService(
        IVoiceContactCenterCallRouter voiceCallRouter,
        IDialerStrategyResolver strategyResolver,
        ILogger<DialerService> logger)
    {
        _voiceCallRouter = voiceCallRouter;
        _strategyResolver = strategyResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RunCycleAsync(DialerProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!profile.Enabled || string.IsNullOrEmpty(profile.QueueId))
        {
            return 0;
        }

        if (profile.Mode is DialerMode.Manual or DialerMode.Preview)
        {
            return 0;
        }

        if (!_voiceCallRouter.CanRouteOutbound(profile.ProviderName))
        {
            _logger.LogWarning("No Contact Center voice provider can route outbound calls for dialer profile '{Profile}'.", profile.Name);

            return 0;
        }

        var strategy = _strategyResolver.Resolve(profile.Mode);

        if (strategy is null)
        {
            _logger.LogWarning(
                "The '{Mode}' dialing mode is not enabled for dialer profile '{Profile}'. Automated dialing was skipped.",
                profile.Mode,
                profile.Name);

            return 0;
        }

        return await strategy.RunCycleAsync(profile, cancellationToken);
    }
}
