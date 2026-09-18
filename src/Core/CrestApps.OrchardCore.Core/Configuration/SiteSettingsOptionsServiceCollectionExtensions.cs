using CrestApps.OrchardCore.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for exposing a tenant's site settings as options.
/// </summary>
public static class SiteSettingsOptionsServiceCollectionExtensions
{
    /// <summary>
    /// Exposes the tenant's site settings of type <typeparamref name="TOptions"/> through
    /// <see cref="IOptionsMonitor{TOptions}"/>, refreshed when the settings are saved.
    /// </summary>
    /// <remarks>
    /// Lets a service that only needs to read configuration depend on options rather than on the
    /// host's settings store. The change-token source is what makes the difference between reading
    /// the settings and caching them forever.
    /// </remarks>
    /// <typeparam name="TOptions">The settings type, which is also the options type.</typeparam>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSiteSettingsOptions<TOptions>(this IServiceCollection services)
        where TOptions : class, new()
    {
        services.AddOptions<TOptions>();
        services.AddTransient<IConfigureOptions<TOptions>, SiteSettingsOptionsConfiguration<TOptions>>();
        services.AddSignalOptionsChangeTokenSource<TOptions>();

        return services;
    }
}
