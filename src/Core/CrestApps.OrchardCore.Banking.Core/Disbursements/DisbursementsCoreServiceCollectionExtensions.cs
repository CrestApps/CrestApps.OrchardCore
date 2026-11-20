using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Handlers;
using CrestApps.OrchardCore.Banking.DirectDeposit;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Banking.Disbursements.Core;

/// <summary>
/// Registers the disbursement services.
/// </summary>
public static class DisbursementsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the disbursement and batch stores, the rules, the manual provider, the bank account usage and the
    /// source access. The encrypted file store is registered by the module, which chooses where it lives.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCatalogs().AddCatalogManagers();
        services.AddNumberSequence();
        services.AddSourceAccessService();
        services.TryAddScoped<IDisbursementStore, DisbursementStore>();
        services.TryAddScoped<IDisbursementBatchStore, DisbursementBatchStore>();
        services.TryAddScoped<IDisbursementManager, DisbursementManager>();
        services.TryAddScoped<IDisbursementBatchManager, DisbursementBatchManager>();
        services.TryAddScoped<ICatalogManager<Disbursement>>(serviceProvider => serviceProvider.GetRequiredService<IDisbursementManager>());
        services.TryAddScoped<ICatalogManager<DisbursementBatch>>(serviceProvider => serviceProvider.GetRequiredService<IDisbursementBatchManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Disbursement>, DisbursementHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<DisbursementBatch>, DisbursementBatchHandler>());
        services.TryAddScoped<IDisbursementCommitter, YesSqlDisbursementCommitter>();
        services.TryAddScoped<IDisbursementService, DisbursementService>();
        services.AddDisbursementProvider<ManualDisbursementProvider>();
        services.AddBankAccountUsage<DisbursementBankAccountUsage>();

        return services;
    }

    /// <summary>
    /// Registers the bank file (Nacha) provider, the prenote verifier and its observer.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementsNacha(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDisbursementProvider<NachaDisbursementProvider>();
        services.AddBankAccountVerifier<NachaPrenoteVerifier>();
        services.AddDisbursementObserver<NachaPrenoteObserver>();

        return services;
    }

    /// <summary>
    /// Registers the check provider and the observer that follows its checks, for when Checks is on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddDisbursementsChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDisbursementProvider<CheckDisbursementProvider>();
        services.AddCheckObserver<CheckDisbursementObserver>();

        return services;
    }
}
