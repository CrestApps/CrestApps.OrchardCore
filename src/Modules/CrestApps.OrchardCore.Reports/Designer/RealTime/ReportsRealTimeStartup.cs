using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.RealTime;

/// <summary>
/// Makes the report builder live as soon as <c>OrchardCore.SignalR</c> is enabled: it shows who else has a report
/// open, and offers to reload when someone else changes, publishes, or discards it.
/// </summary>
[Feature(ReportsConstants.BuilderFeature)]
[RequireFeatures("OrchardCore.SignalR")]
public sealed class ReportsRealTimeStartup : StartupBase
{
    // Runs after DesignerStartup, so its notifier replaces the default one.
    /// <inheritdoc/>
    public override int Order => 100;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IReportDesignNotifier>();
        services.AddScoped<IReportDesignNotifier, SignalRReportDesignNotifier>();
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<ReportsHub>(SignalRHubRoutes.GetHubPath<ReportsHub>());
    }
}

/// <summary>
/// Whether the report builder updates live.
/// </summary>
public static class ReportsRealTime
{
    /// <summary>
    /// Determines whether the builder connects to <see cref="ReportsHub"/>, which it does when the real-time notifier
    /// is registered, that is when <c>OrchardCore.SignalR</c> is enabled.
    /// </summary>
    /// <param name="services">The request services.</param>
    /// <returns><see langword="true"/> when the builder updates live.</returns>
    public static bool IsEnabled(IServiceProvider services)
    {
        return services?.GetService<IReportDesignNotifier>() is SignalRReportDesignNotifier;
    }
}
