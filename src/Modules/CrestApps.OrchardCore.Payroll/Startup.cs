using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Payroll.AuditTrail;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataPipelines;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Deployments;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Navigation;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Settings.Deployment;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Registers Pay Setup: pay schedules and pay codes, their pages and permissions.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
public sealed class PaySetupStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(PayrollConstants.CollectionName));
        services.AddDataMigration<PaySetupMigrations>()
            .AddIndexProvider<PayScheduleIndexProvider>()
            .AddIndexProvider<PayCodeIndexProvider>();

        services.AddPaySetupCore();
        services.AddDisplayDriver<PaySchedule, PayScheduleDisplayDriver>();
        services.AddDisplayDriver<PayCode, PayCodeDisplayDriver>();
        services.AddPermissionProvider<PaySetupPermissionProvider>();
        services.AddNavigationProvider<PaySetupAdminMenu>();
    }
}

/// <summary>
/// Imports pay schedules and pay codes with recipe steps.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class PaySetupRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<PaySchedulesRecipeStep>();
        services.AddRecipeExecutionStep<PayCodesRecipeStep>();
    }
}

/// <summary>
/// Exports pay schedules and pay codes with deployment steps.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class PaySetupDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PaySchedulesDeploymentSource, PaySchedulesDeploymentStep, SummaryDeploymentStepDisplayDriver<PaySchedulesDeploymentStep>>();
        services.AddDeployment<PayCodesDeploymentSource, PayCodesDeploymentStep, SummaryDeploymentStepDisplayDriver<PayCodesDeploymentStep>>();
    }
}

/// <summary>
/// Describes the Pay Setup recipe and deployment steps to CrestApps Recipes.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class PaySetupRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, PaySchedulesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, PayCodesRecipeStepSchema>();
        services.AddDeploymentStepSchema<PaySchedulesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<PayCodesDeploymentStepSchema>();
    }
}

/// <summary>
/// Adds Payroll's data source with the Pay Inputs data set, so a data pipeline exports pay inputs.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class PaySetupDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<PayrollDataSource>();
        services.AddScoped<IPayrollDataSet, PayInputsDataSet>();
    }
}

/// <summary>
/// Adds the Mark pay inputs exported step to Data Pipelines.
/// </summary>
[Feature(PayrollConstants.Features.PaySetup)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelines)]
public sealed class PaySetupDataPipelinesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MarkPayInputsExportedStep>();
        services.Configure<DataPipelineStepTypeOptions>(options => options.Add(DataPipelineStepTypeDescriptor.Create<MarkPayInputsExportedStep>()));
    }
}

/// <summary>
/// Registers the Payroll feature: employers, per-employer permissions, the payroll scope, settings and menu.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
public sealed class PayrollStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPayrollCore();
        services.AddDataMigration<PayrollMigrations>()
            .AddIndexProvider<EmployerIndexProvider>()
            .AddIndexProvider<PayProfileIndexProvider>()
            .AddIndexProvider<PayItemIndexProvider>()
            .AddIndexProvider<PayStatementIndexProvider>()
            .AddIndexProvider<PayStatementLineIndexProvider>();

        services.AddScoped<PayrollNames>();
        services.AddScoped<PayProfileEditTracker>();
        services.AddDisplayDriver<EmployeePayProfile, PayProfileDisplayDriver>();
        services.AddDisplayDriver<EmployeePayItem, PayItemDisplayDriver>();
        services.AddDisplayDriver<StaffMember, StaffPayrollDisplayDriver>();
        services.AddDisplayDriver<PayCode, PayrollPayCodeDisplayDriver>();

        services.AddDisplayDriver<Employer, EmployerDisplayDriver>();
        services.AddPermissionProvider<PayrollPermissionProvider>();
        services.AddNavigationProvider<PayrollAdminMenu>();
        services.AddSiteDisplayDriver<PayrollSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(PayrollConstants.SettingsGroupId, PayrollPermissions.ManagePayrollSettings);
        services.AddPartySource<EmployerPartySource>();
    }
}

/// <summary>
/// Adds Payroll's card to the team editor: the ledger code and a work site's jurisdictions.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(CrestApps.OrchardCore.Teams.TeamsConstants.Features.Teams)]
public sealed class PayrollTeamsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Team, PayrollTeamDisplayDriver>();
    }
}

/// <summary>
/// Writes tax id reveals and changes of pay profiles to the Audit Trail.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.AuditTrail)]
public sealed class PayrollAuditTrailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, PayrollAuditTrailEvents>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayProfileObserver, AuditTrailPayProfileObserver>());
    }
}

/// <summary>
/// Lets an employer choose the bank account it pays from.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(CrestApps.OrchardCore.Banking.BankingConstants.Features.Banking)]
public sealed class PayrollBankingStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<Employer, EmployerFundingDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayProfileBlockerProvider, BankAccountPayBlockers>());
    }
}

/// <summary>
/// Imports employers with a recipe step.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class PayrollRecipesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<EmployersRecipeStep>();
        services.AddRecipeExecutionStep<PayProfilesRecipeStep>();
        services.AddRecipeExecutionStep<PayItemsRecipeStep>();
    }
}

/// <summary>
/// Exports employers and Payroll's settings with deployment steps.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class PayrollDeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<EmployersDeploymentSource, EmployersDeploymentStep, SummaryDeploymentStepDisplayDriver<EmployersDeploymentStep>>();
        services.AddDeployment<PayProfilesDeploymentSource, PayProfilesDeploymentStep, PayProfilesDeploymentStepDisplayDriver>();
        services.AddSiteSettingsPropertyDeploymentStep<PayrollSettings, PayrollDeploymentStartup>(
            S => S["Payroll settings"],
            S => S["Separation of duties and the defaults of a new employer."]);
    }
}

/// <summary>
/// Describes the Payroll recipe and deployment steps and settings to CrestApps Recipes.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class PayrollRecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, EmployersRecipeStepSchema>();
        services.AddDeploymentStepSchema<EmployersDeploymentStepSchema>();
        services.AddScoped<IRecipeStep, PayProfilesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, PayItemsRecipeStepSchema>();
        services.AddDeploymentStepSchema<PayProfilesDeploymentStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, PayrollSettingsSchema>();
    }
}

/// <summary>
/// Adds the Payroll Employees data set to Payroll's data source.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class PayrollDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPayrollDataSet, EmployeesDataSet>();
    }
}
