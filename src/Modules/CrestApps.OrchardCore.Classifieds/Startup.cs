using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Classifieds.BackgroundTasks;
using CrestApps.OrchardCore.Classifieds.Controllers;
using CrestApps.OrchardCore.Classifieds.Drivers;
using CrestApps.OrchardCore.Classifieds.Handlers;
using CrestApps.OrchardCore.Classifieds.Indexes;
using CrestApps.OrchardCore.Classifieds.Migrations;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Classifieds.ViewModels;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Classifieds;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<ClassifiedMigrations>()
            .AddIndexProvider<ClassifiedIndexProvider>();
        services.AddScoped<ClassifiedService>();
        services.AddScoped<ICartLineHandler, ClassifiedUpgradeLineHandler>();
        services.AddScoped<IOrderEventHandler, ClassifiedUpgradeOrderHandler>();
        services.AddSingleton<IBackgroundTask, ClassifiedsBackgroundTask>();

        services.AddContentPart<ClassifiedPart>()
            .UseDisplayDriver<ClassifiedPartDisplayDriver>();
        services.AddScoped<IContentTypePartDefinitionDisplayDriver, ClassifiedPartSettingsDisplayDriver>();
        services.AddContentPart<ClassifiedUpgradePart>()
            .UseDisplayDriver<ClassifiedUpgradePartDisplayDriver>();
        services.AddDisplayDriver<ClassifiedEntry, ClassifiedEntryDisplayDriver>();

        services.AddPermissionProvider<ClassifiedsPermissionProvider>();
        services.AddNavigationProvider<ClassifiedsAdminMenu>();
        services.AddNavigationProvider<ClassifiedsAccountMenu>();
    }
}

/// <summary>
/// Content › Posted listings.
/// </summary>
public sealed class ClassifiedsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _classifieds = new()
    {
        { "area", ClassifiedsConstants.Feature },
        { "controller", typeof(ClassifiedsAdminController).ControllerName() },
        { "action", nameof(ClassifiedsAdminController.Index) },
    };

    internal readonly IStringLocalizer S;

    public ClassifiedsAdminMenu(IStringLocalizer<ClassifiedsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Content"], content => content
                .Add(S["Posted listings"], S["Posted listings"].PrefixPosition("5"), listings => listings
                    .Id("classifieds")
                    .Action(_classifieds)
                    .Permission(ClassifiedsPermissions.ModerateClassifieds)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
