using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the interval statistics: their records and indexes, the roller and its background task, the reader the
/// workforce features use, and the status page.
/// </summary>
[Feature(ContactCenterConstants.Feature.IntervalStatistics)]
public sealed class IntervalStatisticsStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public IntervalStatisticsStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions<IntervalStatisticsOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps:ContactCenter:IntervalStatistics"))
            .Validate(options => options.RetentionDays > 0, "'CrestApps:ContactCenter:IntervalStatistics:RetentionDays' must be greater than zero.")
            .Validate(options => options.BackfillDays >= 0, "'CrestApps:ContactCenter:IntervalStatistics:BackfillDays' cannot be negative.")
            .Validate(options => options.MaxDaysPerRun > 0, "'CrestApps:ContactCenter:IntervalStatistics:MaxDaysPerRun' must be greater than zero.")
            .ValidateOnStart();

        services
            .AddIndexProvider<IntervalStatisticsDayIndexProvider>()
            .AddIndexProvider<QueueIntervalStatisticsIndexProvider>()
            .AddIndexProvider<AgentIntervalStatisticsIndexProvider>()
            .AddDataMigration<IntervalStatisticsIndexMigrations>();

        services.AddScoped<IIntervalStatisticsReader, IntervalStatisticsReader>();
        services.AddScoped<IntervalStatisticsRoller>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, IntervalStatisticsBackgroundTask>());
        services.AddNavigationProvider<IntervalStatisticsAdminMenu>();
    }
}

/// <summary>
/// Registers the data sets of the interval statistics: queue intervals and agent intervals.
/// </summary>
[Feature(ContactCenterConstants.Feature.IntervalStatistics)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class IntervalStatisticsDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, QueueIntervalReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, AgentIntervalReportDataSet>();
    }
}
