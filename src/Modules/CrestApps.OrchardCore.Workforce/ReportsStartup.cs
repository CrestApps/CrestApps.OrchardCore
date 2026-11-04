using CrestApps.OrchardCore.Workforce.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Adds the Workforce report data source.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<WorkforceReportDataSource>();
        services.AddScoped<IWorkforceReportDataSet, WorkersReportDataSet>();
    }
}

/// <summary>
/// Adds the schedule changes data set to the Workforce report data source.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class ShiftPlanningReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, ScheduleChangesReportDataSet>();
    }
}

/// <summary>
/// Adds the time-off data set to the Workforce report data source.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class TimeOffReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, TimeOffRequestsReportDataSet>();
    }
}

/// <summary>
/// Adds the predictability pay data set to the Workforce report data source.
/// </summary>
[Feature(WorkforceConstants.Features.LaborRules)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class LaborRulesReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, PredictabilityPayReportDataSet>();
    }
}

/// <summary>
/// Adds the time clock's data sets to the Workforce report data source.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class TimeClockReportsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkforceReportDataSet, TimePunchesReportDataSet>();
        services.AddScoped<IWorkforceReportDataSet, TimesheetLinesReportDataSet>();
        services.AddScoped<IWorkforceReportDataSet, AttendanceExceptionsReportDataSet>();
    }
}
