namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Provides the default implementation of <see cref="IReportExportManager"/> over the registered
/// export formats.
/// </summary>
public sealed class ReportExportManager : IReportExportManager
{
    private readonly IReadOnlyList<IReportExportFormat> _formats;
    private readonly Dictionary<string, IReportExportFormat> _byName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportManager"/> class.
    /// </summary>
    /// <param name="formats">The registered export formats.</param>
    public ReportExportManager(IEnumerable<IReportExportFormat> formats)
    {
        _byName = new Dictionary<string, IReportExportFormat>(StringComparer.OrdinalIgnoreCase);

        foreach (var format in formats)
        {
            if (!string.IsNullOrEmpty(format.Name))
            {
                if (!_byName.TryAdd(format.Name, format))
                {
                    throw new InvalidOperationException($"A report export format named '{format.Name}' is already registered.");
                }
            }
        }

        _formats = _byName.Values
            .OrderBy(format => format.DisplayName.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IReportExportFormat> ListFormats()
    {
        return _formats;
    }

    /// <inheritdoc/>
    public IReportExportFormat FindFormat(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _byName.GetValueOrDefault(name);
    }
}
