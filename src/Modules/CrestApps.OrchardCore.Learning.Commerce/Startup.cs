using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Core;
using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Drivers;
using CrestApps.OrchardCore.Learning.Commerce.Handlers;
using CrestApps.OrchardCore.Learning.Commerce.Migrations;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Reports;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Commerce.Workflows;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Notifications;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Learning.Commerce;

/// <summary>
/// Registers selling learning: the product part, the cart rule, the order grants and the seats pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options =>
        {
            options.Collections.Add(LearningCommerceConstants.CollectionName);
            options.Collections.Add(LearningCommerceConstants.SeatLedgerCollectionName);
            options.Collections.Add(LearningCommerceConstants.FulfillmentWorkCollectionName);
        });
        services.AddDataMigration<LearningCommerceMigrations>()
            .AddIndexProvider<LearningPurchaseIndexProvider>()
            .AddIndexProvider<LicensePoolIndexProvider>()
            .AddIndexProvider<LicenseSeatIndexProvider>();

        services.AddLearningCommerceCore();
        services.AddContentPart<LearningProductPart>()
            .UseDisplayDriver<LearningProductPartDisplayDriver>();
        services.AddScoped<ICartLineHandler, LearningCartLineHandler>();
        services.AddScoped<IOrderEventHandler, LearningOrderEventHandler>();
        services.AddScoped<LearningPurchaseFulfillment>();
        services.AddScoped<LearningPurchaseProcessor>();
        services.AddSingleton<IBackgroundTask, LearningFulfillmentBackgroundTask>();
        services.AddNavigationProvider<LearningSeatsAccountMenu>();
        services.AddPersonalDataContributor<LearningCommercePersonalDataContributor>();
    }
}

/// <summary>
/// Tells a person a seat was given to them.
/// </summary>
/// <summary>
/// Sells learning paths on their pages when Learning Paths is turned on, also after Learning Commerce.
/// </summary>
[RequireFeatures(ProgramsConstants.Features.Paths)]
public sealed class LearningPathsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<LearningPathProductMigrations>();
    }
}

[RequireFeatures(OrchardCoreFeatureIds.Notifications)]
public sealed class NotificationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNotificationType(LearningCommerceConstants.SeatGivenNotificationType, LocalizationSource.Create<NotificationsStartup>("Course seat given"), type => type.Description = LocalizationSource.Create<NotificationsStartup>("When your account gives you a seat in a course."));
        services.AddLicensePoolObserver<SeatNotificationObserver>();
    }
}

/// <summary>
/// Adds the seat workflow events and the Give Seat task.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLicensePoolObserver<SeatWorkflowObserver>();
        services.AddActivity<SeatsPurchasedEvent, SeatsPurchasedEventDisplayDriver>();
        services.AddActivity<SeatGivenEvent, SeatGivenEventDisplayDriver>();
        services.AddActivity<LicensePoolRunningLowEvent, LicensePoolRunningLowEventDisplayDriver>();
        services.AddActivity<GiveSeatTask, GiveSeatTaskDisplayDriver>();
    }
}

/// <summary>
/// Describes the seat workflow activities to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<SeatsPurchasedEventSchema>()
            .AddWorkflowActivitySchema<SeatGivenEventSchema>()
            .AddWorkflowActivitySchema<LicensePoolRunningLowEventSchema>()
            .AddWorkflowActivitySchema<GiveSeatTaskSchema>();
    }
}

/// <summary>
/// Adds the Learning commerce data source.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<LicensePoolReportDataSet>();
        services.AddScoped<LicenseSeatReportDataSet>();
        services.AddDataSource<LearningCommerceDataSource>();
    }
}
