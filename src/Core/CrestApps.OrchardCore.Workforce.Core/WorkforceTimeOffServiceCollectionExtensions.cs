using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.Core.Handlers;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Registers the time-off services.
/// </summary>
public static class WorkforceTimeOffServiceCollectionExtensions
{
    /// <summary>
    /// Registers time-off types, policies, allotments and blackout dates, the balance ledger and accrual, requests and
    /// approvals, the pending requests on team calendars, and taking approved time off's shifts off the schedule. Call
    /// it with <see cref="WorkforceCoreServiceCollectionExtensions.AddWorkforceCore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWorkforceTimeOffCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TimeOffTypeStore>();
        services.TryAddScoped<TimeOffPolicyStore>();
        services.TryAddScoped<TimeOffLedgerStore>();
        services.TryAddScoped<TimeOffRequestStore>();
        services.TryAddScoped<TimeOffAllotmentStore>();
        services.TryAddScoped<BlackoutPeriodStore>();
        services.TryAddScoped<ITimeOffTypeManager, TimeOffTypeManager>();
        services.TryAddScoped<ITimeOffPolicyManager, TimeOffPolicyManager>();
        services.TryAddScoped<ITimeOffAllotmentManager, TimeOffAllotmentManager>();
        services.TryAddScoped<IBlackoutPeriodManager, BlackoutPeriodManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeOffType>, TimeOffTypeCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeOffPolicy>, TimeOffPolicyCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeOffAllotment>, TimeOffAllotmentCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<BlackoutPeriod>, BlackoutPeriodCatalogHandler>());

        services.TryAddScoped<TimeOffPolicyResolvers>();
        services.TryAddScoped<TimeOffAccrualPlanner>();
        services.TryAddScoped<TimeOffLocks>();
        services.TryAddScoped<WorkerTimeZones>();
        services.TryAddScoped<TimeOffAllotments>();
        services.TryAddScoped<TimeOffApprovals>();
        services.TryAddScoped<IWorkedTimeSource, PublishedShiftWorkedTimeSource>();
        services.TryAddScoped<ITimeOffBalanceService, DefaultTimeOffBalanceService>();
        services.TryAddScoped<ITimeOffAccrualService, DefaultTimeOffAccrualService>();
        services.TryAddScoped<ITimeOffRequestService, DefaultTimeOffRequestService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITimeOffApprovalHandler, TimeOffShiftRemover>());
        services.AddCalendarEventSource<PendingTimeOffEventSource>();

        return services;
    }
}
