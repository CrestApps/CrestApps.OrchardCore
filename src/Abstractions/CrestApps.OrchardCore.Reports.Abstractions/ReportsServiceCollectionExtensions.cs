using CrestApps.OrchardCore.Reports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the extensions of the reports.
/// </summary>
public static class ReportsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a report, as a scoped service, and adds its descriptor to <see cref="ReportOptions"/>.
    /// </summary>
    /// <typeparam name="TReport">The class of the report.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReport<TReport>(this IServiceCollection services)
        where TReport : class, IReport
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TReport>();
        services.Configure<ReportOptions>(options => options.Add(ReportDescriptor.Create<TReport>()));

        return services;
    }

    /// <summary>
    /// Registers a report export format, as a scoped service, and adds its descriptor to
    /// <see cref="ReportExportFormatOptions"/>.
    /// </summary>
    /// <typeparam name="TFormat">The class of the format.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddReportExportFormat<TFormat>(this IServiceCollection services)
        where TFormat : class, IReportExportFormat
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TFormat>();
        services.Configure<ReportExportFormatOptions>(options => options.Add(ReportExportFormatDescriptor.Create<TFormat>()));

        return services;
    }
}
