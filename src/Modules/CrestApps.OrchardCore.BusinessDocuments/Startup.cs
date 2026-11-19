using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Core;
using CrestApps.OrchardCore.BusinessDocuments.Deployments;
using CrestApps.OrchardCore.BusinessDocuments.Drivers;
using CrestApps.OrchardCore.BusinessDocuments.Handlers;
using CrestApps.OrchardCore.BusinessDocuments.Indexes;
using CrestApps.OrchardCore.BusinessDocuments.Migrations;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Navigation;
using CrestApps.OrchardCore.BusinessDocuments.Recipes;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.BusinessDocuments;

/// <summary>
/// Registers document templates, the HTML renderer, the business document shape and the number series settings page.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(BusinessDocumentsStorage.CollectionName));

        services.AddBusinessDocumentsCore();
        services.AddScoped<ICatalogEntryHandler<BusinessDocumentTemplate>, BusinessDocumentTemplateHandler>();

        services
            .AddIndexProvider<BusinessDocumentTemplateIndexProvider>()
            .AddDataMigration<BusinessDocumentsMigrations>();

        services.AddDisplayDriver<BusinessDocumentTemplate, BusinessDocumentTemplateDisplayDriver>();
        services.AddShapeTableProvider<BusinessDocumentShapeTableProvider>();
        services.AddPermissionProvider<BusinessDocumentsPermissionProvider>();
        services.AddNavigationProvider<BusinessDocumentsAdminMenu>();

        services.AddSiteDisplayDriver<NumberSeriesSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(NumberSeriesSettingsDisplayDriver.GroupId, BusinessDocumentsPermissions.ManageNumberSeries);

        services.AddRecipeExecutionStep<BusinessDocumentTemplatesStep>();
    }
}

/// <summary>
/// Document templates travel with deployment plans.
/// </summary>
[Feature(BusinessDocumentsConstants.Features.Area)]
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<BusinessDocumentTemplatesDeploymentSource, BusinessDocumentTemplatesDeploymentStep, SummaryDeploymentStepDisplayDriver<BusinessDocumentTemplatesDeploymentStep>>();
    }
}

/// <summary>
/// Describes the document templates recipe step and the number series settings to CrestApps Recipes.
/// </summary>
[Feature(BusinessDocumentsConstants.Features.Area)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, BusinessDocumentTemplatesRecipeStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, NumberSeriesSettingsSchema>();
    }
}

/// <summary>
/// Registers the PDF renderer. Without the media library, PDFs are written without a logo.
/// </summary>
[Feature(BusinessDocumentsConstants.Features.Pdf)]
public sealed class PdfStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<IBusinessDocumentLogoLoader, NullBusinessDocumentLogoLoader>();
        services.AddScoped<IBusinessDocumentPdfRenderer, BusinessDocumentPdfRenderer>();
    }
}

/// <summary>
/// Reads the logo of a PDF from the media library when it is enabled.
/// </summary>
[Feature(BusinessDocumentsConstants.Features.Pdf)]
[RequireFeatures(OrchardCoreFeatureIds.Media)]
public sealed class PdfMediaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IBusinessDocumentLogoLoader>();
        services.AddScoped<IBusinessDocumentLogoLoader, MediaLibraryLogoLoader>();
    }
}
