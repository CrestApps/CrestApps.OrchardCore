using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Checks.Core.Handlers;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Banking.Checks.Core;

/// <summary>
/// Registers the check services.
/// </summary>
public static class ChecksCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the check register, the layouts, the rules, the printer, the United States check line and English
    /// amounts in words. The encrypted file store is registered by the module, which chooses where it lives.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddChecksCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCatalogs().AddCatalogManagers();
        services.AddNumberSequence();
        services.TryAddScoped<ICheckRecordStore, CheckRecordStore>();
        services.TryAddScoped<ICheckManager, CheckManager>();
        services.TryAddScoped<ICatalogManager<CheckRecord>>(serviceProvider => serviceProvider.GetRequiredService<ICheckManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<CheckRecord>, CheckRecordHandler>());
        services.TryAddScoped<ICheckService, CheckService>();
        services.AddSourceAccessService();
        services.AddBankAccountUsage<CheckBankAccountUsage>();
        services.TryAddScoped<ICheckPrinter, CheckPrinter>();
        services.AddCheckMicrFormat<UnitedStatesCheckMicrFormat>();
        services.AddAmountInWords<EnglishAmountInWords>();

        return services;
    }
}
