using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Warns, on tenant activation, when the base-voice audio path has not been verified for this deployment.
/// </summary>
/// <remarks>
/// The readiness verdict is enforced by <c>ContactCenterBaseVoiceVerificationHealthCheck</c>: a production host
/// withholds readiness until the operator acknowledges the verification. This event only surfaces the same
/// condition in the log so it is visible without probing readiness — <see cref="LogLevel.Critical"/> in a
/// production host where the gate fails closed, and <see cref="LogLevel.Warning"/> outside one where the gate
/// only warns. It never throws and never blocks activation.
/// </remarks>
internal sealed class BaseVoiceVerificationStartupCheck : ModularTenantEvents
{
    private readonly BaseVoiceVerificationOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseVoiceVerificationStartupCheck"/> class.
    /// </summary>
    /// <param name="options">The operator-declared base-voice verification options.</param>
    /// <param name="hostEnvironment">The host environment, used to decide the severity of the warning.</param>
    /// <param name="shellSettings">The tenant shell settings, read for the tenant name.</param>
    /// <param name="logger">The logger.</param>
    public BaseVoiceVerificationStartupCheck(
        IOptions<BaseVoiceVerificationOptions> options,
        IHostEnvironment hostEnvironment,
        ShellSettings shellSettings,
        ILogger<BaseVoiceVerificationStartupCheck> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task ActivatedAsync()
    {
        if (_options.AudioVerificationAcknowledged)
        {
            return Task.CompletedTask;
        }

        if (_hostEnvironment.IsProduction())
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    "Tenant '{TenantName}' is running in a production host but the base-voice audio path has not been verified. Contact Center voice readiness is withheld until 'CrestApps_ContactCenter:BaseVoiceVerification:AudioVerificationAcknowledged' is set to 'true' after the base-voice deployment acceptance step passes.",
                    _shellSettings.Name);
            }

            return Task.CompletedTask;
        }

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "Tenant '{TenantName}': the base-voice audio path has not been verified. This is tolerated outside a production host, but a production host withholds voice readiness until the base-voice deployment acceptance step is acknowledged.",
                _shellSettings.Name);
        }

        return Task.CompletedTask;
    }
}
