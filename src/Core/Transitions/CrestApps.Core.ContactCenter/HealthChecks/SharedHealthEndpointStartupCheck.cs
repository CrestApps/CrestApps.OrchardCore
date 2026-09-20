using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.HealthChecks;

/// <summary>
/// Records, once at start, whether a shared aggregate health endpoint is named as this deployment's liveness
/// probe while the Contact Center is running behind it.
/// </summary>
/// <remarks>
/// The hazard is that an aggregate endpoint reports unhealthy when any one dependency is unhealthy, so a
/// deployment that points its liveness probe at one will have its healthy nodes restarted whenever a
/// dependency wobbles. The check reports rather than throws, for the reason the contract gives: refusing to
/// start takes away the screens an operator would use to fix it.
/// </remarks>
public sealed class SharedHealthEndpointStartupCheck : IContactCenterStartupCheck
{
    private readonly SharedHealthEndpointHazardState _state;
    private readonly ISharedHealthEndpointDescriptor _descriptor;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedHealthEndpointStartupCheck"/> class.
    /// </summary>
    /// <param name="state">The holder the verdict is recorded in, which a health check reads.</param>
    /// <param name="descriptor">The host's description of its own shared health endpoint.</param>
    /// <param name="logger">The logger.</param>
    public SharedHealthEndpointStartupCheck(
        SharedHealthEndpointHazardState state,
        ISharedHealthEndpointDescriptor descriptor,
        ILogger<SharedHealthEndpointStartupCheck> logger)
    {
        _state = state;
        _descriptor = descriptor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "Contact Center shared health endpoint";

    /// <inheritdoc/>
    public Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var hazardMessage = SharedHealthCheckEndpointGuard.BuildHazardMessage(
            _descriptor.Route,
            _descriptor.IsAcknowledged);

        _state.Record(hazardMessage);

        if (hazardMessage is not null && _logger.IsEnabled(LogLevel.Critical))
        {
            _logger.LogCritical("The shared health-check endpoint is misconfigured. {HazardMessage}", hazardMessage);
        }

        return Task.CompletedTask;
    }
}
