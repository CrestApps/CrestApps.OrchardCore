using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Records, on tenant activation, whether the shared <c>OrchardCore.HealthChecks</c> aggregate endpoint is named
/// as a liveness probe while Contact Center is enabled.
/// </summary>
/// <remarks>
/// Validation never throws. Throwing during activation bricks the tenant with no diagnostic surface — the admin,
/// the one place the operator would look, becomes unreachable, and the shared endpoint's shipped-default route
/// already claims liveness. Instead the verdict is recorded, a <see cref="LogLevel.Critical"/> entry explains the
/// hazard, and a dependency health check surfaces it. The tenant stays reachable so the route can be corrected.
/// </remarks>
internal sealed class SharedHealthCheckEndpointValidator : ModularTenantEvents
{
    private readonly SharedHealthEndpointHazardState _state;
    private readonly IShellConfiguration _shellConfiguration;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedHealthCheckEndpointValidator"/> class.
    /// </summary>
    /// <param name="state">The per-tenant holder the hazard verdict is recorded in.</param>
    /// <param name="shellConfiguration">The shell configuration used to read both modules' health settings.</param>
    /// <param name="shellSettings">The tenant shell settings, used to name the tenant in diagnostics.</param>
    /// <param name="logger">The logger.</param>
    public SharedHealthCheckEndpointValidator(
        SharedHealthEndpointHazardState state,
        IShellConfiguration shellConfiguration,
        ShellSettings shellSettings,
        ILogger<SharedHealthCheckEndpointValidator> logger)
    {
        _state = state;
        _shellConfiguration = shellConfiguration;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task ActivatedAsync()
    {
        var hazardMessage = SharedHealthCheckEndpointGuard.BuildHazardMessage(
            _shellConfiguration["OrchardCore_HealthChecks:Url"],
            string.Equals(
                _shellConfiguration["CrestApps_ContactCenter:HealthChecks:AllowUnsafeSharedEndpointRoute"],
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase));

        _state.Record(hazardMessage);

        if (hazardMessage is not null && _logger.IsEnabled(LogLevel.Critical))
        {
            _logger.LogCritical(
                "The shared health-check endpoint is misconfigured for tenant '{TenantName}'. {HazardMessage}",
                _shellSettings.Name,
                hazardMessage);
        }

        return Task.CompletedTask;
    }
}
