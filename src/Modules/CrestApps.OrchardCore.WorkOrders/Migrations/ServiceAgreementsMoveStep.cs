using System.Globalization;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.WorkOrders.Migrations;

/// <summary>
/// Moves the service agreements an upgraded tenant kept in Work Orders to the Agreements module, in the background and
/// again every minute until every one of them has an agreement, then gives the roles that managed service agreements
/// the agreement permission. A copy that fails is logged and tried again on the next run; nothing is lost.
/// </summary>
internal sealed class ServiceAgreementsMoveStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "WorkOrders.ServiceAgreements";

    /// <summary>
    /// The permission Work Orders had for its own agreements.
    /// </summary>
    internal const string LegacyPermission = "ManageServiceAgreements";

    private readonly ServiceAgreementUpgrade _upgrade;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    public ServiceAgreementsMoveStep(ServiceAgreementUpgrade upgrade, IServiceProvider services, ILogger<ServiceAgreementsMoveStep> logger)
    {
        _upgrade = upgrade;
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (moved, total, failed) = await _upgrade.RunAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Moved {Moved} of {Total} service agreements to the Agreements module; {Failed} failed and are tried again.", moved, total, failed);
        }

        // The roles are granted once, after the agreements moved; granting again changes nothing.
        await RolePermissionMigrations.GrantAsync(
            _services,
            new Dictionary<string, string[]>
            {
                [LegacyPermission] = [AgreementsPermissions.ManageAgreements.Name],
            },
            _logger);

        context.Cursor = moved.ToString(CultureInfo.InvariantCulture);

        // Done once every agreement has moved; a failed one keeps the step for the next run.
        return failed == 0;
    }
}
