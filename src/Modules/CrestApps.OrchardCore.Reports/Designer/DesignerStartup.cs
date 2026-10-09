using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Handlers;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Registers the report designer: the query engine, the stores of designed reports, views, and share links, the
/// built-in views data source, the sharing-aware authorization handler, and the admin menu.
/// </summary>
[Feature(ReportsConstants.DesignerFeature)]
public sealed class DesignerStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesignerStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant configuration, which can override the designer size limits.</param>
    public DesignerStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCatalogs();

        services.Configure<ReportQueryLimits>(_shellConfiguration.GetSection("CrestApps:Reports:Designer:Limits"));

        services
            .AddScoped<IReportDataSourceManager, ReportDataSourceManager>()
            .AddScoped<ReportQueryPlanner>()
            .AddScoped<ReportQueryEngine>()
            .AddScoped<ReportValueFormatter>()
            .AddScoped<ReportDesignDocumentBuilder>()
            .AddScoped(sp => new Lazy<ReportQueryPlanner>(sp.GetRequiredService<ReportQueryPlanner>))
            .AddScoped(sp => new Lazy<ReportQueryEngine>(sp.GetRequiredService<ReportQueryEngine>))
            .AddScoped<ReportExecutionContextFactory>()
            .AddScoped<ReportOwnerPrincipalResolver>()
            .AddScoped<ReportDesignService>()
            .AddScoped<ReportDesignRunner>()
            .AddScoped<ReportShareLinkService>()
            .AddScoped<DesignedReportPresenter>()
            .AddScoped<IReportDataSource, ReportViewsDataSource>();

        services.TryAddScoped(sp => new Lazy<IAuthorizationService>(sp.GetRequiredService<IAuthorizationService>));
        services.AddScoped<IAuthorizationHandler, ReportDesignAuthorizationHandler>();
        services.AddPermissionProvider<ReportDesignerPermissionProvider>();
        services.AddNavigationProvider<ReportDesignerAdminMenu>();
    }
}
