using CrestApps.OrchardCore.Reports;

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
    {
        _byName = new Dictionary<string, IReport>(StringComparer.OrdinalIgnoreCase);

        foreach (var report in reports)
        {
            if (!string.IsNullOrEmpty(report.Name))
            {
                if (!_byName.TryAdd(report.Name, report))
                {
                    throw new InvalidOperationException($"A report named '{report.Name}' is already registered.");
                }
            }
        }

        _reports = _byName.Values
            .OrderBy(report => report.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(report => report.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IReport> ListReports()
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
}
