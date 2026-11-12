using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Staff;

/// <summary>
/// Registration helpers for modules that build on Staff.
/// </summary>
public static class StaffServiceCollectionExtensions
{
    /// <summary>
    /// Registers a capability: a kind of work staff members can be marked for, shown as a checkbox in the staff editor
    /// and usable as a picker filter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The capability name stored on staff members.</param>
    /// <param name="displayName">The name shown.</param>
    /// <param name="description">What it lets the person do.</param>
    /// <param name="position">The order in the editor.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffCapability(this IServiceCollection services, string name, LocalizationSource displayName, LocalizationSource description = null, int position = 0)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<StaffOptions>(options => options.Capabilities[name] = new StaffCapability
        {
            Name = name,
            DisplayName = displayName,
            Description = description,
            Position = position,
        });

        return services;
    }

    /// <summary>
    /// Lets holders of another module's permissions manage staff: everyone, or only the staff they manage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="manageEveryone">A permission that lets a person manage every staff member, or <see langword="null"/>.</param>
    /// <param name="manageOwn">A permission that lets a person manage the staff they manage, or <see langword="null"/>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffScopePermissions(this IServiceCollection services, Permission manageEveryone, Permission manageOwn)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<StaffOptions>(options =>
        {
            if (manageEveryone is not null)
            {
                options.ManageEveryonePermissions.Add(manageEveryone);
            }

            if (manageOwn is not null)
            {
                options.ManageOwnPermissions.Add(manageOwn);
            }
        });

        return services;
    }

    /// <summary>
    /// Registers an observer of staff changes.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStaffLifecycleObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, IStaffLifecycleObserver
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStaffLifecycleObserver, TObserver>());

        return services;
    }
}
