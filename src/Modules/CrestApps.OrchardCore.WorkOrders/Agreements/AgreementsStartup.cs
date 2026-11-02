using CrestApps.OrchardCore.WorkOrders.Agreements.Drivers;
using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Migrations;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// Registers service agreements, their pages and the task that makes their work orders.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
public sealed class AgreementsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddServiceAgreementsCore();
        services.AddPermissionProvider<AgreementsPermissionsProvider>();
        services.AddIndexProvider<ServiceAgreementIndexProvider>();
        services.AddDataMigration<ServiceAgreementsMigrations>();
        services.AddDisplayDriver<ServiceAgreementRow, ServiceAgreementRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ServiceAgreementBackgroundTask>());
        services.AddNavigationProvider<AgreementsAdminMenu>();
    }
}

/// <summary>
/// Registers the expiring workflow event.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class AgreementsWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IServiceAgreementObserver, WorkflowServiceAgreementObserver>();
        services.AddActivity<ServiceAgreementExpiringEvent, ServiceAgreementExpiringEventDisplayDriver>();
    }
}

/// <summary>
/// Provides the service agreement permission.
/// </summary>
internal sealed class AgreementsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkOrdersPermissions.ManageServiceAgreements,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = global::OrchardCore.OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}

/// <summary>
/// Makes the work orders of service agreements ahead of their visits.
/// </summary>
[BackgroundTask(
    Title = "Service agreement visits",
    Schedule = "5 * * * *",
    Description = "Makes the work orders of active service agreements ahead of their visits, once each, and expires agreements past their end.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class ServiceAgreementBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<CrestApps.OrchardCore.WorkOrders.Core.Services.ServiceAgreementScheduler>().RunAsync(cancellationToken);
}

/// <summary>
/// Adds Service Agreements to the Field Service menu.
/// </summary>
public sealed class AgreementsAdminMenu : AdminNavigationProvider
{
    internal readonly Microsoft.Extensions.Localization.IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgreementsAdminMenu(Microsoft.Extensions.Localization.IStringLocalizer<AgreementsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Field Service"], fieldService => fieldService
            .Add(S["Service Agreements"], S["Service Agreements"].PrefixPosition("20"), agreements => agreements
                .Id("serviceAgreements")
                .Action("Index", "ServiceAgreements", WorkOrdersConstants.Features.WorkOrders)
                .Permission(WorkOrdersPermissions.ManageServiceAgreements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
