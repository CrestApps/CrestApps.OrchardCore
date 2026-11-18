using CrestApps.Core.Services;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the agreement services.
/// </summary>
public static class AgreementsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the agreement store, manager, balance and life cycle. The feature that imports agreements registers their
    /// rules (<c>AgreementHandler</c>), so a recipe and the editor check the same set.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddAgreementsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<AgreementStore>();
        services.TryAddScoped<ICatalog<Agreement>>(provider => provider.GetRequiredService<AgreementStore>());
        services.TryAddScoped<AgreementManager>();
        services.TryAddScoped<IAgreementManager>(provider => provider.GetRequiredService<AgreementManager>());
        services.TryAddScoped<ICatalogManager<Agreement>>(provider => provider.GetRequiredService<AgreementManager>());
        services.TryAddScoped<IAgreementBalanceService, AgreementBalanceService>();
        services.TryAddScoped<AgreementLifecycle>();

        return services;
    }
}
