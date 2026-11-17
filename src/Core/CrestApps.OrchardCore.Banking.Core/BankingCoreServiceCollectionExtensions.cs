using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Core.Handlers;
using CrestApps.OrchardCore.Banking.Core.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Banking.Core;

/// <summary>
/// Registers the bank account services.
/// </summary>
public static class BankingCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bank account store, manager, rules and the United States routing check.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddBankingCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSensitiveValueProtector();
        services.TryAddScoped<BankAccountChangeTracker>();
        services.TryAddScoped<IBankAccountStore, BankAccountStore>();
        services.TryAddScoped<IBankAccountManager, BankAccountManager>();
        services.TryAddScoped<ICatalogManager<BankAccount>>(serviceProvider => serviceProvider.GetRequiredService<IBankAccountManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<BankAccount>, BankAccountHandler>());
        services.TryAddScoped<IBankAccountService, BankAccountService>();
        services.AddBankRoutingValidator<UnitedStatesBankRoutingValidator>();

        return services;
    }
}
