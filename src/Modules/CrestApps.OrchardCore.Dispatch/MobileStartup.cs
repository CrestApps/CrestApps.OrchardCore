using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Migrations;
using CrestApps.OrchardCore.Dispatch.Mobile;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Registers the technician app: the Field page of My Work, kept offline, its sync and location endpoints, photos
/// and signatures in the media library, and the retention of positions.
/// </summary>
[Feature(DispatchConstants.Features.Mobile)]
public sealed class MobileStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDispatchMobileCore();
        services.AddTransient<IConfigureOptions<DispatchMobileOptions>, DispatchMobileOptionsConfiguration>();
        services
            .AddIndexProvider<FieldCommandReceiptIndexProvider>()
            .AddIndexProvider<WorkerLocationConsentIndexProvider>()
            .AddIndexProvider<WorkerLocationPingIndexProvider>()
            .AddDataMigration<FieldMigrations>();

        services.AddScoped<IFieldFileStore, MediaFieldFileStore>();
        services.AddScoped<IMyWorkLinkProvider, FieldLinkProvider>();
        services.Configure<MyWorkOfflineOptions>(options => options.Pages.Add("~/Admin/my-work/field"));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, FieldRetentionBackgroundTask>());
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapFieldEndpoints();
    }
}

/// <summary>
/// Lists Inventory's stock locations in the technician app, for parts used.
/// </summary>
[Feature(DispatchConstants.Features.Mobile)]
[RequireFeatures("CrestApps.OrchardCore.Inventory")]
public sealed class MobileInventoryStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFieldStockSource, InventoryFieldStockSource>();
    }
}

/// <summary>
/// Shows the assets of the technician's work orders, with their history, in the technician app.
/// </summary>
[Feature(DispatchConstants.Features.Mobile)]
[RequireFeatures("CrestApps.OrchardCore.Assets")]
public sealed class MobileAssetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IFieldAssetSource, AssetsFieldAssetSource>();
    }
}
