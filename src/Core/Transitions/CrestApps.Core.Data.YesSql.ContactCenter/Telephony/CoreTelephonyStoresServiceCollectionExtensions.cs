using CrestApps.Core.Data.YesSql.Telephony.Indexes;
using CrestApps.Core.Data.YesSql.Telephony.Services;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Telephony;

/// <summary>
/// Registers the YesSql persistence behind the telephony services.
/// </summary>
/// <remarks>
/// These are kept apart from the services that read through them, so a host that stores its telephony data
/// somewhere else registers its own implementations of the same contracts and calls nothing here.
/// </remarks>
public static class CoreTelephonyStoresServiceCollectionExtensions
{
    /// <summary>
    /// Registers the YesSql telephony stores and the index providers that keep their tables in step.
    /// </summary>
    /// <remarks>
    /// The user-connection index is not here. It indexes the host's own user documents, so only a host can
    /// describe it.
    /// </remarks>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddCoreTelephonyStoresYesSql(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ITelephonyInteractionStore, DefaultTelephonyInteractionStore>();
        services.AddScoped<ITelephonyExtensionStore, TelephonyExtensionStore>();

        services.AddSingleton<IIndexProvider, TelephonyExtensionIndexProvider>();
        services.AddSingleton<IIndexProvider, TelephonyInteractionIndexProvider>();

        return services;
    }
}
