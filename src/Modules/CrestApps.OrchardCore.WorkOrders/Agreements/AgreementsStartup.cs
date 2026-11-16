using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.WorkOrders.Agreements.Drivers;
using CrestApps.OrchardCore.WorkOrders.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// The field-service terms of agreements: their visit schedules on the agreement editor, the task that makes their work
/// orders, and their billable labor counted against the agreement's hours.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
public sealed class AgreementsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddServiceAgreementsCore();
        services.AddDataMigration<ServiceAgreementsMigrations>();
        services.AddDisplayDriver<Agreement, WorkOrderAgreementTermsDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ServiceAgreementBackgroundTask>());
        services.AddNavigationProvider<AgreementsAdminMenu>();
    }
}

/// <summary>
/// Starts the expiring workflow event of agreements with visits.
/// </summary>
[Feature(WorkOrdersConstants.Features.Agreements)]
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class AgreementsWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAgreementObserver, WorkflowServiceAgreementObserver>();
        services.AddActivity<ServiceAgreementExpiringEvent, ServiceAgreementExpiringEventDisplayDriver>();
    }
}

/// <summary>
/// Makes the work orders of agreements ahead of their visits.
/// </summary>
[BackgroundTask(
    Title = "Service agreement visits",
    Schedule = "5 * * * *",
    Description = "Makes the work orders of the visits of active agreements ahead of time, once each.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class ServiceAgreementBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<CrestApps.OrchardCore.WorkOrders.Core.Services.ServiceAgreementScheduler>().RunAsync(cancellationToken), "created"));
}

/// <summary>
/// Adds the agreements to the Field Service menu.
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
                .Action("Index", "Agreements", AgreementsConstants.Features.Agreements)
                .Permission(AgreementsPermissions.ViewAgreements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
