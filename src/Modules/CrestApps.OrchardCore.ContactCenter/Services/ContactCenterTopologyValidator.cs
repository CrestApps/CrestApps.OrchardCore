using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
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
                    "Unable to validate the Contact Center deployment topology for tenant '{TenantName}': {Error} Contact Center work admission is refused until this is resolved.",
                    _shellSettings.Name,
                    OperationalLogRedactor.RedactException(ex));
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

        if (result.IsProductionTopology && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Tenant '{TenantName}' satisfies the production Contact Center topology '{ProfileId}'.",
                _shellSettings.Name,
                result.DeclaredProfileId);
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
