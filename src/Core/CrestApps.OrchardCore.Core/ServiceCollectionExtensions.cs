using CrestApps.Core.Hosting;
using CrestApps.Core.Locking;
using CrestApps.Core.Security;
using CrestApps.Core.Sms;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Hosting;
using CrestApps.OrchardCore.Core.Sms;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the catalogs.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(Catalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceCatalog<>));

        return services;
    }

    /// <summary>
    /// Registers the <see cref="TimeProvider"/> used by the CrestApps services, backed by the Orchard Core clock.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreTimeProvider(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider, ClockTimeProviderAdapter>();

        return services;
    }

    /// <summary>
    /// Binds the framework host seams to their Orchard Core implementations, so framework services
    /// get the tenant's distributed lock, tenant name and users rather than the single-node defaults.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreHostSeams(this IServiceCollection services)
    {
        services.AddCoreTimeProvider();
        services.AddCoreUserDirectory();
        services.TryAddSingleton<IDistributedLockProvider, OrchardCoreDistributedLockProvider>();
        services.TryAddSingleton<ITenantAccessor, ShellSettingsTenantAccessor>();
        services.TryAddScoped<IPublicBaseUrlAccessor, SiteSettingsPublicBaseUrlAccessor>();
        services.TryAddScoped<IDetachedWorkExecutor, ShellDetachedWorkExecutor>();
        services.TryAddScoped<IAfterCommitTaskQueue, ShellScopeAfterCommitTaskQueue>();
        services.TryAddScoped<IScopedWorkExecutor, ShellScopedWorkExecutor>();

        return services;
    }

    /// <summary>
    /// Binds the framework SMS provider seam to the providers Orchard Core has registered.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreSmsProviderSeam(this IServiceCollection services)
    {
        services.TryAddScoped<ISmsProviderResolver, OrchardCoreSmsProviderResolver>();

        return services;
    }

    /// <summary>
    /// Binds the framework user seams to Orchard Core's users.
    /// </summary>
    /// <remarks>
    /// Registration only, so a tenant that never reads a user never resolves the user manager. Folded
    /// into <see cref="AddCoreHostSeams(IServiceCollection)"/> as well, because a feature that reads
    /// users should not have to know it needs a second call.
    /// </remarks>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddCoreUserDirectory(this IServiceCollection services)
    {
        // The ambient-principal accessor ships in CrestApps.Core but nothing in this repository
        // registered it, so the contract was unusable until now.
        services.TryAddScoped<IUserAccessor, CrestApps.Core.Services.UserAccessor>();
        services.TryAddScoped<IUserDirectory, OrchardCoreUserDirectory>();
        services.TryAddScoped<IUserProfileStore, OrchardCoreUserProfileStore>();

        return services;
    }
}
