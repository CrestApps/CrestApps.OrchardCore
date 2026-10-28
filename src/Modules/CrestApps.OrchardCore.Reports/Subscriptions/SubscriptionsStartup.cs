using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports.Subscriptions;

/// <summary>
/// Registers report subscriptions: reports emailed on a schedule for the period before.
/// </summary>
[Feature(ReportsConstants.SubscriptionsFeature)]
public sealed class SubscriptionsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ReportSubscriptionMigrations>()
            .AddIndexProvider<ReportSubscriptionIndexProvider>();
        services.AddScoped<IReportSubscriptionStore, ReportSubscriptionStore>();
        services.AddScoped<ReportSubscriptionService>();
        services.AddSingleton<IBackgroundTask, ReportSubscriptionsBackgroundTask>();
        services.AddPermissionProvider<ReportSubscriptionPermissionProvider>();
        services.AddNavigationProvider<ReportSubscriptionsAdminMenu>();
        services.AddDisplayDriver<ReportSubscription, ReportSubscriptionDisplayDriver>();
    }
}

public static class ReportSubscriptionPermissions
{
    /// <summary>
    /// Adds and changes report subscriptions. Each report also needs its own permission to choose it.
    /// </summary>
    public static readonly Permission ManageReportSubscriptions = new("ManageReportSubscriptions", LocalizationSource.Create("Manage report subscriptions", typeof(ReportSubscriptionPermissions)));
}

internal sealed class ReportSubscriptionPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions = [ReportSubscriptionPermissions.ManageReportSubscriptions];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        => [new PermissionStereotype { Name = OrchardCoreConstants.Roles.Administrator, Permissions = _allPermissions }];
}

/// <summary>
/// Reports › Subscriptions.
/// </summary>
public sealed class ReportSubscriptionsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public ReportSubscriptionsAdminMenu(IStringLocalizer<ReportSubscriptionsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Reports"], "after.40", reports => reports
                .Add(S["Subscriptions"], "after", subscriptions => subscriptions
                    .AddClass("report-subscriptions")
                    .Id("reportSubscriptions")
                    .Action(nameof(Controllers.ReportSubscriptionsController.Index), typeof(Controllers.ReportSubscriptionsController).ControllerName(), new { area = ReportsConstants.Feature })
                    .Permission(ReportSubscriptionPermissions.ManageReportSubscriptions)
                    .LocalNav()), priority: ReportsConstants.AdminMenuPriority);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Every five minutes, emails the subscriptions whose time has come, and schedules each one's next.
/// </summary>
[BackgroundTask(
    Title = "Report subscriptions",
    Schedule = "*/5 * * * *",
    Description = "Emails scheduled reports for the period before.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class ReportSubscriptionsBackgroundTask : IBackgroundTask
{
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ReportSubscriptionsBackgroundTask>>();

        try
        {
            var now = serviceProvider.GetRequiredService<IClock>().UtcNow;
            var service = serviceProvider.GetRequiredService<ReportSubscriptionService>();

            foreach (var subscription in await serviceProvider.GetRequiredService<IReportSubscriptionStore>().ListDueAsync(now, cancellationToken))
            {
                // A run is for its scheduled time, so a late one still covers the period it was due for.
                await service.SendAsync(subscription, subscription.NextRunUtc ?? now, reschedule: true, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while emailing report subscriptions.");
        }
    }
}

/// <summary>
/// Renders a subscription as a row of the subscriptions admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class ReportSubscriptionDisplayDriver : DisplayDriver<ReportSubscription>
{
    public override IDisplayResult Display(ReportSubscription subscription, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = subscription.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("ReportSubscription_Fields_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ReportSubscription_Description_SummaryAdmin", subscription)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Schedule:1"),
            View("ReportSubscription_Buttons_SummaryAdmin", subscription).Location("Actions:5"));
    }
}
