using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Core.Handlers;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Teams.Core;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Staff.Core;

/// <summary>
/// Registers the Staff services.
/// </summary>
public static class StaffCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the staff store, manager, directory, hierarchy, manager scoping, time zones, history, badges and the
    /// Staff role, and makes Staff the source of team members. It is safe to call more than once, so a module that
    /// builds on staff can call it from its own registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTeamsCore();
        services.AddOptions<StaffOptions>();
        services.AddOptions<StaffRoleOptions>();
        services.TryAddScoped<StaffMemberStore>();
        services.TryAddScoped<StaffHistoryStore>();
        services.TryAddScoped<IStaffMemberManager, StaffMemberManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<StaffMember>, StaffMemberCatalogHandler>());
        services.TryAddScoped<IStaffDirectory, StaffDirectory>();
        services.TryAddScoped<StaffHierarchy>();
        services.TryAddScoped<IStaffHierarchy>(sp => sp.GetRequiredService<StaffHierarchy>());
        services.TryAddScoped<IStaffScopeService, StaffScopeService>();
        services.TryAddScoped<IStaffTimeZoneResolver, StaffTimeZoneResolver>();
        services.TryAddScoped<IStaffHistoryService, StaffHistoryService>();
        services.TryAddScoped<IStaffBadgeService, StaffBadgeService>();
        services.TryAddScoped<StaffAccounts>();
        services.TryAddScoped<IStaffAccountService>(sp => sp.GetRequiredService<StaffAccounts>());
        services.TryAddScoped<StaffRoles>();
        services.AddTeamMemberProvider<StaffTeamMemberProvider>();
        services.TryAddScoped<StaffTeamReader>();
        services.AddStaffLifecycleObserver<StaffRoleLifecycleObserver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserAudienceProvider, StaffUserAudienceProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserAudienceProvider, StaffUnlinkedUserAudienceProvider>());

        return services;
    }

    /// <summary>
    /// Makes a staff member and their user one skill holder, so skills given to either count for both. Call it when
    /// Skills is on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffSkillLinks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISkillSubjectLinkProvider, StaffSkillSubjectLinkProvider>());

        return services;
    }
}
