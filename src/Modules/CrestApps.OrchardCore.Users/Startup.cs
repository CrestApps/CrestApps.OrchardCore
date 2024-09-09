using CrestApps.OrchardCore.Users.Core;
using CrestApps.OrchardCore.Users.Core.Handlers;
using CrestApps.OrchardCore.Users.Core.Models;
using CrestApps.OrchardCore.Users.Core.Services;
using CrestApps.OrchardCore.Users.Drivers;
using CrestApps.OrchardCore.Users.Filters;
using CrestApps.OrchardCore.Users.Indexes;
using CrestApps.OrchardCore.Users.Migrations;
using CrestApps.OrchardCore.Users.Models;
using CrestApps.OrchardCore.Users.Recipes;
using CrestApps.OrchardCore.Users.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentFields.Drivers;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Contents.Drivers;
using OrchardCore.Contents.Models;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes.Services;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Users;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddUserCacheService();
        services.TryAddBasicDisplayNameProvider();
        services.AddScoped<IDisplayDriver<UserMenu>, DisplayNameUserMenuDisplayDriver>();
        services.AddScoped<IDisplayDriver<UserBadgeContext>, UserBadgeNameDisplayDriver>();
        services.AddScoped<IShapeTableProvider, DisplayUserShapeTableProvider>();
    }
}

[RequireFeatures("OrchardCore.Liquid")]
public sealed class LiquidStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddLiquidFilter<DisplayUserFullNameFilter>("display_name");
    }
}

[RequireFeatures("OrchardCore.Contents")]
public sealed class CoreContentUserStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IShapeTableProvider, ContentShapeTableProvider>();
        services.AddScoped<IContentDisplayDriver, UserContentsDriver>();
    }
}

[RequireFeatures("OrchardCore.DynamicCache")]
public sealed class CashingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUserEventHandler, DefaultUserEventHandler>();
    }
}

[Feature(UsersConstants.Feature.DisplayName)]
public sealed class DisplayNameStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.PostConfigure<DisplayUserOptions>(options =>
        {
            options.ConvertAuthorToShape = true;
        });

        var oldProvider = services.FirstOrDefault(x => x.ServiceType == typeof(IUserPickerResultProvider) && x.ImplementationType == typeof(DefaultUserPickerResultProvider));

        if (oldProvider is not null)
        {
            services.Remove(oldProvider);
        }

        services.AddScoped<IUserPickerResultProvider, DisplayNameUserPickerResultProvider>();

        services.AddDisplayNameProvider();
        services.AddContentPart<UserFullNamePart>();
        services.AddScoped<IDisplayDriver<User>, UserFullNamePartDisplayDriver>();

        services.AddDataMigration<UserFullNameMigrations>();
        services.AddIndexProvider<UserFullNameIndexProvider>();

        services.AddScoped<IPermissionProvider, UserDisplayNamePermissionsProvider>();

        services.AddSiteDisplayDriver<DisplayNameSettingsDisplayDriver>();
        services.AddNavigationProvider<UserDisplayNameAdminMenu>();

        services.AddContentField<UserPickerField>()
            .RemoveDisplayDriver<UserPickerFieldDisplayDriver>()
            .UseDisplayDriver<DisplayNameUserPickerFieldDisplayDriver>(d => !string.Equals(d, "UserNames", StringComparison.OrdinalIgnoreCase));

        services.AddContentPart<CommonPart>()
            .ForEditor<PermissionDefinedEditorDriver>(editor => editor == PermissionDefinedEditorDriver.PermissionDefinedEditor)
            .ForEditor<OwnerEditorDriver>(editor => string.IsNullOrWhiteSpace(editor) || editor.Equals("Standard", StringComparison.OrdinalIgnoreCase));
    }
}

[RequireFeatures(UserConstants.Features.Users)]
public sealed class UsersStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStepHandler, UpdateUserRecipeStepHandler>();
    }
}

[Feature(UsersConstants.Feature.DisplayName)]
[RequireFeatures(UserConstants.Features.Users)]
public sealed class UserOverrideStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IUsersAdminListFilterProvider, DisplayNameUsersAdminListFilterProvider>();
        services.Configure<UsersAdminListFilterOptions>(options =>
        {
            options.TermName = DisplayNameUsersAdminListFilterProvider.DefaultTermName;
        });
    }
}

[Feature(UsersConstants.Feature.Avatars)]
public sealed class AvatarStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.PostConfigure<DisplayUserOptions>(options =>
        {
            options.ConvertAuthorToShape = true;
        });

        services.AddContentPart<UserAvatarPart>();
        services.AddScoped<IDisplayDriver<User>, UserAvatarPartDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, AvatarResourceManagementOptionsConfiguration>();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<AvatarStylesFilter>();
        });
        services.AddScoped<IPermissionProvider, AvatarPermissionsProvider>();
        services.AddNavigationProvider<AvatarAdminMenu>();
        services.AddTransient<IConfigureOptions<UserAvatarOptions>, UserAvatarOptionsConfiguration>();
        services.AddSiteDisplayDriver<UserAvatarOptionsDisplayDriver>();
        services.AddScoped<IDisplayDriver<UserBadgeContext>, UserBadgeAvatarDisplayDriver>();
        services.AddScoped<IShapeTableProvider, AvatarUserShapeTableProvider>();
    }
}
