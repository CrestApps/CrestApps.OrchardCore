using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Describes a registered export format with the static members of its class, so formats are listed without creating
/// them. <c>services.AddReportExportFormat&lt;TFormat&gt;()</c> registers one per format.
/// </summary>
public sealed class ReportExportFormatDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportFormatDescriptor"/> class.
    /// </summary>
    /// <param name="name">The stable technical name of the format.</param>
    /// <param name="displayName">The label shown to users.</param>
    /// <param name="extension">The file extension of the format, with its leading dot.</param>
    /// <param name="contentType">The media type of the format.</param>
    /// <param name="type">The class of the format, resolved from the services when a report is exported.</param>
    public ReportExportFormatDescriptor(string name, LocalizationSource displayName, string extension, string contentType, Type type)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(type);

        Name = name;
        DisplayName = displayName;
        Extension = extension;
        ContentType = contentType;
        Type = type;
    }

    /// <summary>
    /// Gets the stable technical name of the format.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the label shown to users, to localize with <c>IStringLocalizerFactory.Localize</c>.
    /// </summary>
    public LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the file extension of the format, with its leading dot, such as <c>.csv</c>.
    /// </summary>
    public string Extension { get; }

    /// <summary>
    /// Gets the media type of the format, such as <c>text/csv</c>.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the class of the format, which implements <see cref="IReportExportFormat"/>.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Describes a format from the static members of its class.
    /// </summary>
    /// <typeparam name="TFormat">The class of the format.</typeparam>
    /// <returns>The descriptor.</returns>
    public static ReportExportFormatDescriptor Create<TFormat>()
        where TFormat : class, IReportExportFormat
    {
        return new ReportExportFormatDescriptor(TFormat.Name, TFormat.DisplayName, TFormat.Extension, TFormat.ContentType, typeof(TFormat));
    }
}

/// <summary>
/// Holds the registered export formats. <c>services.AddReportExportFormat&lt;TFormat&gt;()</c> adds them.
/// </summary>
public sealed class ReportExportFormatOptions
{
    private readonly List<ReportExportFormatDescriptor> _formats = [];

    /// <summary>
    /// Gets the registered export formats, in the order they were registered.
    /// </summary>
    public IReadOnlyList<ReportExportFormatDescriptor> Formats => _formats;

    /// <summary>
    /// Adds an export format, unless its class is already registered.
    /// </summary>
    /// <param name="descriptor">The descriptor of the format.</param>
    /// <returns>The options.</returns>
    public ReportExportFormatOptions Add(ReportExportFormatDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_formats.Any(format => format.Type == descriptor.Type))
        {
            _formats.Add(descriptor);
        }

        return this;
    }
}
