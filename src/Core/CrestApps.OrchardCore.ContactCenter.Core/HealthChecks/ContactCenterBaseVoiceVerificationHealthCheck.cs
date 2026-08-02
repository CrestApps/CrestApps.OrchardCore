using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;

/// <summary>
/// Reports whether the operator has verified the base-voice audio path for this deployment.
/// </summary>
/// <remarks>
/// This is a readiness check that observes a condition every node shares, and — like
/// <see cref="ContactCenterTopologyHealthCheck"/> — the exception is deliberate. Whether the WebRTC media path
/// works is fixed infrastructure that no amount of waiting repairs, and serving voice traffic from a deployment
/// whose base-voice path was never proven is the failure being prevented, not collateral damage. In a
/// production host environment an unacknowledged deployment therefore fails readiness closed. Outside a
/// production host environment the gate does not withhold readiness, so development and test hosts are not
/// blocked; the accompanying startup warning covers those cases.
/// </remarks>
public sealed class ContactCenterBaseVoiceVerificationHealthCheck : IHealthCheck
{
    private readonly BaseVoiceVerificationOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterBaseVoiceVerificationHealthCheck"/> class.
    /// </summary>
    /// <param name="options">The operator-declared base-voice verification options.</param>
    /// <param name="hostEnvironment">The host environment, used to decide whether the gate fails closed.</param>
    public ContactCenterBaseVoiceVerificationHealthCheck(
        IOptions<BaseVoiceVerificationOptions> options,
        IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(Evaluate(
            context,
            _options.AudioVerificationAcknowledged,
            _hostEnvironment.IsProduction(),
            _options.AudioVerificationEvidenceReference));
    }

    /// <summary>
    /// Decides the readiness verdict from the declared verification state.
    /// </summary>
    /// <param name="context">The health check context supplying the configured failure status.</param>
    /// <param name="acknowledged">Whether the operator has acknowledged the base-voice audio verification.</param>
    /// <param name="isProductionEnvironment">Whether the host is running in a production environment.</param>
    /// <param name="evidenceReference">An optional reference to the retained verification evidence.</param>
    /// <returns>The readiness verdict for this deployment.</returns>
    public static HealthCheckResult Evaluate(
        HealthCheckContext context,
        bool acknowledged,
        bool isProductionEnvironment,
        string evidenceReference)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (acknowledged)
        {
            return string.IsNullOrWhiteSpace(evidenceReference)
                ? HealthCheckResult.Healthy("The base-voice audio path has been verified and acknowledged for this deployment.")
                : HealthCheckResult.Healthy($"The base-voice audio path has been verified and acknowledged for this deployment (evidence: {evidenceReference}).");
        }

        if (isProductionEnvironment)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "The base-voice audio path has not been verified for this production deployment. " +
                "Complete the base-voice deployment acceptance step and set " +
                "'CrestApps_ContactCenter:BaseVoiceVerification:AudioVerificationAcknowledged' to 'true'. " +
                "Readiness is withheld until it is acknowledged.");
        }

        return HealthCheckResult.Healthy(
            "The base-voice audio path has not been verified for this deployment. " +
            "This is tolerated outside a production host environment; a production host withholds readiness until it is acknowledged.");
    }
}
