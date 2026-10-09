using CrestApps.OrchardCore.TenantHierarchy.BackgroundTasks;
using CrestApps.OrchardCore.TenantHierarchy.Core.Indexes;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Drivers;
using CrestApps.OrchardCore.TenantHierarchy.Handlers;
using CrestApps.OrchardCore.TenantHierarchy.Migrations;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Admin.Models;
using OrchardCore.AuditTrail.Models;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Setup;
using OrchardCore.Users.Events;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.TenantHierarchy;

/// <summary>
/// Registers the services of the Platform feature, which runs in the Default tenant only.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Platform)]
public sealed class PlatformStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TenantHierarchyPlatformService>();
        services.AddPermissionProvider<PlatformPermissionProvider>();
        services.AddNavigationProvider<PlatformAdminMenu>();
    }
}

/// <summary>
/// Registers the services of the Parent feature: the child tenants admin, access grants, delegated access and the
/// activity log.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Parent)]
public sealed class ParentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSetup();

        AddCommonServices(services);

        services
            .AddScoped<ChildTenantEntryStore>()
            .AddScoped<AccessGrantStore>()
            .AddScoped<DelegatedAccessCodeStore>()
            .AddScoped<DelegatedAccessSessionStore>()
            .AddScoped<HierarchyAuditLog>()
            .AddScoped<TenantSwitcherPreferenceStore>()
            .AddScoped<HierarchyLabelsProvider>()
            .AddScoped<ChildTenantManager>()
            .AddScoped<AccessGrantManager>()
            .AddScoped<DelegatedAccessIssuer>();

        services
            .AddIndexProvider<ChildTenantEntryIndexProvider>()
            .AddIndexProvider<AccessGrantIndexProvider>()
            .AddIndexProvider<DelegatedAccessCodeIndexProvider>()
            .AddIndexProvider<DelegatedAccessSessionIndexProvider>()
            .AddIndexProvider<TenantSwitcherPreferenceIndexProvider>()
            .AddDataMigration<ParentTenantMigrations>();

        services.AddScoped<IUserClaimsProvider, ParentSessionClaimsProvider>();
        services.AddTransient<IPostConfigureOptions<CookieAuthenticationOptions>, ParentCookieEventsSetup>();
        services.AddSingleton<IBackgroundTask, ChildTenantMaintenanceBackgroundTask>();

        services.AddPermissionProvider<ParentPermissionProvider>();
        services.AddNavigationProvider<ParentAdminMenu>();
        services.AddDisplayDriver<Navbar, ParentSwitcherNavbarDisplayDriver>();
        services.AddDisplayDriver<AuditTrailEvent, HierarchyAuditTrailEventDisplayDriver>();
        services.AddTransient<IConfigureOptions<AuditTrailOptions>, TenantHierarchyAuditTrailEventConfiguration>();
    }

    internal static void AddCommonServices(IServiceCollection services)
    {
        services.TryAddScoped<ITenantHierarchyBroker, TenantHierarchyBroker>();
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(TenantHierarchyConstants.CollectionName));
    }
}

/// <summary>
/// Registers the services of the Child feature: the delegated access sign-in, linked users and their protections,
/// session validation and the tenant switcher.
/// </summary>
[Feature(TenantHierarchyConstants.Features.Child)]
public sealed class ChildStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        ParentStartup.AddCommonServices(services);

        services
            .AddScoped<UserLinkStore>()
            .AddScoped<LinkedUserService>()
            .AddScoped<DelegatedSessionPrincipalValidator>();

        services
            .AddIndexProvider<UserLinkIndexProvider>()
            .AddDataMigration<ChildTenantMigrations>();

        services.AddTransient<IPostConfigureOptions<CookieAuthenticationOptions>, ChildCookieEventsSetup>();
        services.AddScoped<ILoginFormEvent, LinkedUserLoginFormEvent>();
        services.AddScoped<IUserEventHandler, LinkedUserEventHandler>();
        services.AddScoped<IAuthorizationHandler, LinkedUserAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, LocalPermissionDenyAuthorizationHandler>();
        services.AddDisplayDriver<Navbar, TenantSwitcherNavbarDisplayDriver>();
    }
}
