using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core;

/// <summary>
/// Registers the services of the Payroll feature.
/// </summary>
public static class PayrollCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers employers, the payroll scope and the employee directory, on top of Pay Setup.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPaySetupCore();
        services.AddCatalogs().AddCatalogManagers();
        services.TryAddScoped<EmployerStore>();
        services.TryAddScoped<IEmployerManager, EmployerManager>();
        services.TryAddScoped<ICatalogManager<Employer>>(serviceProvider => serviceProvider.GetRequiredService<IEmployerManager>());
        services.TryAddScoped<IPayrollScopeService, PayrollScopeService>();
        services.AddSensitiveValueProtector();
        services.TryAddScoped<PayProfileStore>();
        services.TryAddScoped<PayItemStore>();
        services.TryAddScoped<IPayProfileManager, PayProfileManager>();
        services.TryAddScoped<IPayItemManager, PayItemManager>();
        services.TryAddScoped<ICatalogManager<EmployeePayProfile>>(serviceProvider => serviceProvider.GetRequiredService<IPayProfileManager>());
        services.TryAddScoped<ICatalogManager<EmployeePayItem>>(serviceProvider => serviceProvider.GetRequiredService<IPayItemManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EmployeePayProfile>, PayProfileHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EmployeePayItem>, PayItemHandler>());
        services.TryAddScoped<PayrollTaxIdHasher>();
        services.TryAddScoped<DefaultPayProfileService>();
        services.TryAddScoped<IPayProfileService>(serviceProvider => serviceProvider.GetRequiredService<DefaultPayProfileService>());
        services.TryAddScoped<IPayrollEmployeeDirectory, PayProfileEmployeeDirectory>();
        services.TryAddScoped<PayProfileUsage>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IEmployerUsage, PayProfileUsage>(serviceProvider => serviceProvider.GetRequiredService<PayProfileUsage>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPaySetupUsage, PayProfileUsage>(serviceProvider => serviceProvider.GetRequiredService<PayProfileUsage>()));

        services.TryAddScoped<PayrollPayCodeSeeder>();
        services.TryAddScoped<PayStatementStore>();
        services.TryAddScoped<IPayAccumulatorService, PayAccumulatorService>();
        services.TryAddScoped<IPayCalculator, PayCalculator>();
        services.TryAddScoped<PayCalculationContextFactory>();
        services.AddNumberSequence();
        services.TryAddScoped<PayRunStore>();
        services.TryAddScoped<PayRunInputStore>();
        services.TryAddScoped<IPayRunManager, PayRunManager>();
        services.TryAddScoped<PayStatementReviewService>();
        services.TryAddScoped<IPayrollCommitter, YesSqlPayrollCommitter>();
        services.TryAddScoped<IPayRunInputManager, PayRunInputManager>();
        services.TryAddScoped<ICatalogManager<PayRun>>(serviceProvider => serviceProvider.GetRequiredService<IPayRunManager>());
        services.TryAddScoped<ICatalogManager<PayRunInput>>(serviceProvider => serviceProvider.GetRequiredService<IPayRunInputManager>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayRun>, PayRunHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayRunInput>, PayRunInputHandler>());
        services.TryAddScoped<DefaultPayRunService>();
        services.TryAddScoped<IPayRunService>(serviceProvider => serviceProvider.GetRequiredService<DefaultPayRunService>());
        services.AddApprovalType<PayRunApprovalTypeHandler>(PayrollConstants.PayRunSubjectKind, type =>
        {
            type.DisplayName = LocalizationSource.Create<PayRunApprovalTypeHandler>("Pay run");
            type.Description = LocalizationSource.Create<PayRunApprovalTypeHandler>("A pay run of an employer, approved before it is processed.");
        });
        services.AddSourceAccess<PayRunSourceAccess>();
        services.AddSourceAccess<PayStatementSourceAccess>();
        services.TryAddScoped<PayRunInputImporter>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, EarningsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, RetroPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, TipCreditPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, RegularRatePayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, EarnedRegularRatePayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, PreTaxDeductionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, TaxesPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, PostTaxDeductionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, DisposableEarningsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, NetCheckPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, ContributionsPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, AfterTaxMatchesPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayCalculationStep, DistributionPayCalculationStep>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPaySetupUsage, PayCodeBaseUsage>());

        return services;
    }

    /// <summary>
    /// Registers Direct Deposit: net pay deposited to bank accounts through Disbursements.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollDirectDepositCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementPaymentMethod, DirectDepositPaymentMethod>());

        return services;
    }

    /// <summary>
    /// Registers Paychecks: net pay paid by checks through Checks, with the statement printed as the voucher.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPayrollPaychecksCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayStatementPaymentMethod, PaycheckPaymentMethod>());
        services.AddCheckVoucherSource<PayStatementCheckVoucherSource>();

        return services;
    }
}
