using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// The default <see cref="IReportManager"/>, built from the reports of <see cref="ReportOptions"/>. It lists them from
/// their descriptors, and creates a report from the services of the scope only when it is run.
/// </summary>
public sealed class ReportManager : IReportManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<ReportDescriptor> _reports;
    private readonly Dictionary<string, ReportDescriptor> _byName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportManager"/> class.
    /// </summary>
    /// <param name="options">The registered reports.</param>
    /// <param name="serviceProvider">The services of the scope, which reports are created from.</param>
    public ReportManager(IOptions<ReportOptions> options, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _serviceProvider = serviceProvider;
        _reports = options.Value.Reports;
        _byName = new Dictionary<string, ReportDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var report in _reports)
        {
            if (!_byName.TryAdd(report.Name, report))
            {
                throw new InvalidOperationException($"More than one report is registered with the name '{report.Name}'.");
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ReportDescriptor> GetReports() => _reports;

    /// <inheritdoc/>
    public ReportDescriptor GetDescriptor(string name)
    {
        return string.IsNullOrEmpty(name) ? null : _byName.GetValueOrDefault(name);
    }

    /// <inheritdoc/>
    public IReportRunner GetReport(string name)
    {
        return GetDescriptor(name)?.Factory(_serviceProvider);
    }
}
