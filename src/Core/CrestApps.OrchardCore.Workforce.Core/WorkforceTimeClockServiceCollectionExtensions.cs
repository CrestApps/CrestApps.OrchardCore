using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Core.Handlers;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Registers the time clock's services.
/// </summary>
public static class WorkforceTimeClockServiceCollectionExtensions
{
    /// <summary>
    /// Adds punches, timesheets, attendance and payroll export. Worked time for per-hour accrual comes from punches
    /// from then on.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddWorkforceTimeClockCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TimeClockPolicyStore>();
        services.TryAddScoped<PayPeriodCalendarStore>();
        services.TryAddScoped<PayCodeStore>();
        services.TryAddScoped<TimeClockKioskStore>();
        services.TryAddScoped<TimePunchStore>();
        services.TryAddScoped<TimesheetStore>();
        services.TryAddScoped<AttendanceExceptionStore>();
        services.TryAddScoped<PunchCorrectionStore>();
        services.TryAddScoped<PayrollExportBatchStore>();
        services.TryAddScoped<PayrollCsvSettingsStore>();
        services.TryAddScoped<ITimeClockPolicyManager, TimeClockPolicyManager>();
        services.TryAddScoped<IPayPeriodCalendarManager, PayPeriodCalendarManager>();
        services.TryAddScoped<IPayCodeManager, PayCodeManager>();
        services.TryAddScoped<ITimeClockKioskManager, TimeClockKioskManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeClockPolicy>, TimeClockPolicyCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayPeriodCalendar>, PayPeriodCalendarCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<PayCode>, PayCodeCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<TimeClockKiosk>, TimeClockKioskCatalogHandler>());
        services.TryAddScoped<TimeOffLocks>();
        services.TryAddScoped<WorkerTimeZones>();
        services.TryAddSingleton<ITimesheetCalculator, DefaultTimesheetCalculator>();
        services.TryAddScoped<ITimeClockService, DefaultTimeClockService>();
        services.TryAddScoped<ITimesheetService, DefaultTimesheetService>();
        services.TryAddScoped<IAttendanceService, DefaultAttendanceService>();
        services.TryAddScoped<IPayrollExportService, DefaultPayrollExportService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayrollExportProvider, CsvPayrollExportProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAttendancePointsHandler, ManagerPointsNotice>());
        services.TryAddScoped<TimesheetRefresher>();
        services.TryAddScoped<PayCodeSeeder>();
        services.Replace(ServiceDescriptor.Scoped<IWorkedTimeSource, TimeClockWorkedTimeSource>());

        return services;
    }
}
