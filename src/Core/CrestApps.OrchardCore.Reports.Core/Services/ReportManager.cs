namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Provides the default implementation of <see cref="IReportManager"/> over the registered reports.
/// </summary>
public sealed class ReportManager : IReportManager
{
    private readonly IReadOnlyList<IReport> _reports;
    private readonly Dictionary<string, IReport> _byName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportManager"/> class.
    /// </summary>
    /// <param name="reports">The registered reports.</param>
    public ReportManager(IEnumerable<IReport> reports)
        : this(reports, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportManager"/> class.
    /// </summary>
    /// <param name="reports">The reports registered individually.</param>
    /// <param name="reportProviders">The providers that each contribute a family of reports.</param>
    public ReportManager(
        IEnumerable<IReport> reports,
        IEnumerable<IReportProvider> reportProviders)
    {
        _byName = new Dictionary<string, IReport>(StringComparer.OrdinalIgnoreCase);

        foreach (var report in reports)
        {
            AddReport(report);
        }

        foreach (var reportProvider in reportProviders)
        {
            foreach (var report in reportProvider.GetReports())
            {
                AddReport(report);
            }
        }

        _reports = _byName.Values
            .OrderBy(report => report.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(report => report.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IReport> GetReports()
    {
        return _reports;
    }

    /// <inheritdoc/>
    public IReport FindByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _byName.GetValueOrDefault(name);
    }

    private void AddReport(IReport report)
    {
        if (string.IsNullOrEmpty(report.Name))
        {
            return;
        }

        if (!_byName.TryAdd(report.Name, report))
        {
            throw new InvalidOperationException($"A report named '{report.Name}' is already registered.");
        }
    }
}
