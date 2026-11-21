using OrchardCore.Localization;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Drivers;
using CrestApps.OrchardCore.Dispatch.Migrations;
using CrestApps.OrchardCore.Dispatch.Navigation;
using CrestApps.OrchardCore.Dispatch.RealTime;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.Dispatch.Workflows;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Scheduling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Puts the field service KPIs in the Reports menu while the Reports module is on.
/// </summary>
[RequireFeatures(ReportsConstants.Feature)]
public sealed class ReportsMenuStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<FieldServiceReportsAdminMenu>();
    }
}
