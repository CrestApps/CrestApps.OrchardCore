using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Core.Handlers;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Banking.Checks.Core.Handlers;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Payroll.Core.Handlers;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Registers the catalog entry handlers the module startups add next to the Core services, for harnesses that build
/// their services from the Core extensions alone.
/// </summary>
internal static class CatalogEntryHandlers
{
    public static IServiceCollection AddPaySetupHandlers(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PaySchedule>, PayScheduleHandler>());

        return services;
    }

    public static IServiceCollection AddPayrollHandlers(this IServiceCollection services)
    {
        services.AddPaySetupHandlers();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Employer>, EmployerHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TaxDeposit>, TaxDepositHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<CrestApps.OrchardCore.Taxation.Models.TaxForm>, CrestApps.OrchardCore.Taxation.Core.TaxForms.TaxFormHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<IncomeTaxRule>, IncomeTaxRuleHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<GarnishmentOrder>, GarnishmentOrderHandler>());

        return services;
    }

    public static IServiceCollection AddApprovalsHandlers(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ApprovalPolicy>, ApprovalPolicyHandler>());

        return services;
    }

    public static IServiceCollection AddChecksHandlers(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<CheckLayout>, CheckLayoutHandler>());

        return services;
    }
}
