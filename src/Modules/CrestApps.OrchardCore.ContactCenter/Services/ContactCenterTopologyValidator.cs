using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Validates, on tenant activation, that this deployment satisfies the topology its operator declared.
/// </summary>
/// <remarks>
/// Validation never throws. Throwing during activation bricks the tenant with no diagnostic surface — the
/// admin cannot be reached to read the error, which is the one place the operator would look. Instead the
/// verdict is recorded, a <see cref="LogLevel.Critical"/> entry names every missing component, the readiness
/// probe reports unhealthy, and Contact Center work admission is refused. The tenant stays reachable so the
/// configuration can be corrected.
/// </remarks>
internal sealed class ContactCenterTopologyValidator : ModularTenantEvents
{
    private readonly ContactCenterTopologyState _state;
    private readonly ContactCenterTopologyOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTopologyValidator"/> class.
    /// </summary>
    /// <param name="state">The per-tenant holder the verdict is recorded in.</param>
    /// <param name="options">The operator-declared topology options.</param>
    /// <param name="shellSettings">The tenant shell settings, read for the configured database provider.</param>
    /// <param name="shellFeaturesManager">The feature manager used to observe which features are enabled.</param>
    /// <param name="distributedLock">The resolved distributed lock, inspected for a process-local implementation.</param>
    /// <param name="hostEnvironment">The host environment, used to reject an undeclared production deployment.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterTopologyValidator(
        ContactCenterTopologyState state,
        IOptions<ContactCenterTopologyOptions> options,
        ShellSettings shellSettings,
        IShellFeaturesManager shellFeaturesManager,
        IDistributedLock distributedLock,
        IHostEnvironment hostEnvironment,
        ILogger<ContactCenterTopologyValidator> logger)
    {
        _state = state;
        _options = options.Value;
        _shellSettings = shellSettings;
        _shellFeaturesManager = shellFeaturesManager;
        _distributedLock = distributedLock;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ActivatedAsync()
    {
        ContactCenterTopologyValidationResult result;

        try
        {
            result = ContactCenterTopologyEvaluator.Evaluate(await ObserveAsync());
        }
        catch (Exception ex)
        {
            // An observation that cannot be taken is itself a failure to validate. Recording a satisfied verdict
            // here would make an infrastructure fault indistinguishable from a supported deployment.
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    ex,
                    "Unable to validate the Contact Center deployment topology for tenant '{TenantName}': Contact Center work admission is refused until this is resolved.",
                    _shellSettings.Name);
            }

            _state.Record(new ContactCenterTopologyValidationResult
            {
                DeclaredProfileId = _options.ProfileId,
                IsProductionTopology = false,
                Failures = ["The Contact Center deployment topology could not be validated."],
            });

            return;
        }

        _state.Record(result);

        if (!result.IsSatisfied)
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    "Tenant '{TenantName}' declares the Contact Center topology '{ProfileId}' but does not satisfy it: {Failures} Contact Center work admission is refused and this node reports unready until the deployment is corrected.",
                    _shellSettings.Name,
                    result.DeclaredProfileId ?? "none",
                    string.Join(" ", result.Failures));
            }

            return;
        }

        if (result.IsProductionTopology)
        {
            var profile = ContactCenterTopologyProfiles.Find(result.DeclaredProfileId);

            if (profile is { MaximumApplicationNodes: 1 })
            {
                // A single-active-node production profile carries a constraint no probe on this node can enforce:
                // topology validation confirms the declared infrastructure prerequisites but never counts how many
                // application nodes are actually running, so a second active node claiming the same real-time voice
                // application is not detected. Emitting the operator responsibility on the one-time activation log
                // — the surface operators read for activation-time deployment facts — keeps it visible without
                // leaking topology detail onto the anonymous readiness probe or muddying the readiness/dependency
                // health-check separation. It is logged at Warning so the caveat survives the Warning default
                // minimum level shipped by the production host; the ordinary satisfied-topology message stays at
                // Information.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Tenant '{TenantName}' satisfies the production Contact Center topology '{ProfileId}', which certifies exactly one active application node. This node cannot detect a second active node claiming the same real-time voice application, so running a single active node is an operator responsibility. See docs/telephony/asterisk.md.",
                        _shellSettings.Name,
                        result.DeclaredProfileId);
                }
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Tenant '{TenantName}' satisfies the production Contact Center topology '{ProfileId}'.",
                    _shellSettings.Name,
                    result.DeclaredProfileId);
            }
        }
    }

    private async Task<ContactCenterTopologyObservations> ObserveAsync()
    {
        var enabledFeatureIds = (await _shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ContactCenterTopologyObservations
        {
            DeclaredProfileId = _options.ProfileId,
            IsProductionHostEnvironment = _hostEnvironment.IsProduction(),
            DatabaseProvider = _shellSettings["DatabaseProvider"],
            RedisFeatureEnabled = enabledFeatureIds.Contains(ContactCenterTopologyEvaluator.RedisFeatureId),
            RedisLockFeatureEnabled = enabledFeatureIds.Contains(ContactCenterTopologyEvaluator.RedisLockFeatureId),
            SignalRRedisBackplaneFeatureEnabled = enabledFeatureIds.Contains(ContactCenterTopologyEvaluator.SignalRRedisBackplaneFeatureId),
            DistributedLockIsProcessLocal = _distributedLock is ILocalLock,
        };
    }
}
