using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AdminDashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that lists the admin dashboard widgets, when the Admin Dashboard feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.AdminDashboard)]
public sealed class AdminDashboardToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminDashboardToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminDashboardToolsStartup(IStringLocalizer<AdminDashboardToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAdminDashboardWidgetsTool>(ListAdminDashboardWidgetsTool.TheName)
            .WithTitle(S["List Admin Dashboard Widgets"])
            .WithDescription(S["Lists the widgets on the admin dashboard with their content type, position, width and height, and the content types that can be added as widgets. Use it before adding or rearranging widgets."])
            .WithCategory(S["Admin"])
            .Selectable();
    }
}
