using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// The default <see cref="IReportExportManager"/>: lists the formats of <see cref="ReportExportFormatOptions"/> and
/// resolves a format from the services when a report is exported.
/// </summary>
public sealed class ReportExportManager : IReportExportManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<ReportExportFormatDescriptor> _formats;
    private readonly Dictionary<string, ReportExportFormatDescriptor> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportManager"/> class.
    /// </summary>
    /// <param name="options">The registered export formats.</param>
    /// <param name="serviceProvider">The services the formats are resolved from.</param>
    public ReportExportManager(IOptions<ReportExportFormatOptions> options, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _serviceProvider = serviceProvider;
        _formats = options.Value.Formats;

        foreach (var format in _formats)
        {
            if (!_byName.TryAdd(format.Name, format))
            {
                throw new InvalidOperationException($"A report export format named '{format.Name}' is already registered.");
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ReportExportFormatDescriptor> GetFormats() => _formats;

    /// <inheritdoc/>
    public ReportExportFormatDescriptor GetDescriptor(string name)
    {
        return string.IsNullOrEmpty(name) ? null : _byName.GetValueOrDefault(name);
    }

    /// <inheritdoc/>
    public IReportExportFormat GetFormat(string name)
    {
        return GetDescriptor(name) is { } descriptor
            ? (IReportExportFormat)_serviceProvider.GetRequiredService(descriptor.Type)
            : null;
    }
}
