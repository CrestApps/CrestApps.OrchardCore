using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Omnichannel.Managements.Recipes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Contents.Services;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the CRM: leads, accounts and opportunities.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
public sealed class CrmStartup : StartupBase
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer for the names this feature contributes.</param>
    public CrmStartup(IStringLocalizer<CrmStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        // Lead types exist as a concept only while this feature is on; everything outside the feature reads this.
        services.Configure<OmnichannelCrmOptions>(options => options.Enabled = true);

        services.AddContentPart<LeadPart>()
            .UseDisplayDriver<LeadPartDisplayDriver>()
            .AddHandler<LeadPartHandler>();

        services.AddContentPart<AccountPart>()
            .AddHandler<AccountPartHandler>();

        services.AddContentPart<OpportunityPart>()
            .UseDisplayDriver<OpportunityPartDisplayDriver>()
            .AddHandler<OpportunityPartHandler>();

        services.AddScoped<IContentTypePartDefinitionDisplayDriver, LeadPartSettingsDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, OpportunityPartSettingsDisplayDriver>();
        services.AddScoped<IContentDisplayDriver, AccountPickerDisplayDriver>();

        services
            .AddIndexProvider<LeadIndexProvider>()
            .AddIndexProvider<OpportunityIndexProvider>()
            .AddDataMigration<CrmMigrations>();

        services.AddScoped<ICatalogEntryHandler<LeadStatus>, LeadStatusHandler>();
        services.AddScoped<ICatalogEntryHandler<OpportunityStage>, OpportunityStageHandler>();
        services.AddDisplayDriver<LeadStatus, LeadStatusDisplayDriver>();
        services.AddDisplayDriver<OpportunityStage, OpportunityStageDisplayDriver>();

        services.AddScoped<CrmCatalogSeeder>();
        services.AddScoped<LeadStatusFlagService>();
        services.AddScoped<LeadRatingProvider>();
        services.AddScoped<CrmAccountListSynchronizer>();
        services.AddScoped<IContentDefinitionEventHandler, CrmContentDefinitionEventHandler>();

        services.AddNavigationProvider<CrmAdminMenu>();
        services.AddPermissionProvider<CrmPermissionProvider>();
        services.AddTransient<IContentsAdminListFilterProvider, CrmContentsAdminListFilterProvider>();
        services.AddShapeTableProvider<AccountListPartShapeTableProvider>();

        services.AddScoped<ILeadConversionService, LeadConversionService>();
        services.AddScoped<LeadMatchFinder>();
        services.AddScoped<ISubjectActionHandler, ConvertLeadSubjectActionHandler>();
        services.AddDisplayDriver<SubjectAction, LeadSubjectActionDisplayDriver>();
        services.AddDisplayDriver<OmnichannelActivityBatch, LeadBatchFilterDisplayDriver>();

        services.Configure<SubjectActionOptions>(options =>
        {
            options.AddActionType(OmnichannelConstants.ActionTypes.ConvertLead, entry =>
            {
                entry.DisplayName = S["Convert Lead"];
                entry.Description = S["Converts the activity's lead into a contact, and optionally an account and an opportunity. The actions after it work on the contact."];
            });
        });
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddCrmSearchEndpoints();
    }
}

/// <summary>
/// Registers the deployment steps that export the CRM catalogs.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class CrmDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<LeadStatusDeploymentSource, LeadStatusDeploymentStep>();
        services.AddDeployment<OpportunityStageDeploymentSource, OpportunityStageDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, LeadStatusDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, OpportunityStageDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe steps that import the CRM catalogs.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class CrmRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<LeadStatusStep>();
        services.AddRecipeExecutionStep<OpportunityStageStep>();
        services.AddScoped<global::OrchardCore.Recipes.Events.IRecipeEventHandler, CrmRecipeEventHandler>();
    }
}

/// <summary>
/// Registers the lead and account columns of the content import and export.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures(ContentTransferConstants.Feature.ModuleId)]
public sealed class CrmContentTransferStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPartImportHandler<LeadPart, LeadPartContentImportHandler>();
        services.AddScoped<IContentImportHandler, AccountContentImportHandler>();
        services.AddScoped<IContentImportHandler, LeadExportFilterHandler>();
        services.AddDisplayDriver<ExportRequest, LeadExportOptionsDisplayDriver>();
    }
}

/// <summary>
/// Registers the lead and pipeline reports.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures(CrestApps.OrchardCore.Reports.ReportsConstants.Feature)]
public sealed class CrmReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<CrestApps.OrchardCore.Reports.IReport, Reports.LeadFunnelReportProvider>()
            .AddScoped<CrestApps.OrchardCore.Reports.IReport, Reports.LeadConversionReportProvider>()
            .AddScoped<CrestApps.OrchardCore.Reports.IReport, Reports.OpportunityPipelineReportProvider>();
    }
}

/// <summary>
/// Registers the Lead Converted workflow event and the Convert Lead workflow task, when Orchard Core Workflows is on.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class CrmWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<Workflows.Models.LeadConvertedEvent, Workflows.Drivers.LeadConvertedEventDisplayDriver>();
        services.AddActivity<Workflows.Models.ConvertLeadTask, Workflows.Drivers.ConvertLeadTaskDisplayDriver>();
        services.AddScoped<ILeadConversionHandler, Workflows.LeadConvertedWorkflowHandler>();
    }
}
