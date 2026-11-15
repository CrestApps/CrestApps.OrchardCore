using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.YesSql.Core.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Learning.Commerce.Core;

/// <summary>
/// Registers the services of Learning commerce.
/// </summary>
public static class LearningCommerceCoreServiceCollectionExtensions
{
    public static IServiceCollection AddLearningCommerceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<LearningPurchaseStore>();
        services.TryAddScoped<LicensePoolStore>();
        services.TryAddScoped<LicenseSeatStore>();
        services.AddQuantityLedger<SeatLedger>(options => options.CollectionName = LearningCommerceConstants.SeatLedgerCollectionName);
        services.TryAddScoped<ILicensePoolService, DefaultLicensePoolService>();
        services.AddAccountInvitationObserver<LicenseSeatInvitationObserver>();

        return services;
    }
}
