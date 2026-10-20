using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Describes a registered report, so reports are listed, routed to and authorized without creating them.
/// <c>services.AddReport&lt;TReport&gt;()</c> describes an <see cref="IReport"/> with the static members of its class;
/// a module that runs a family of reports with one class adds a descriptor per report to <see cref="ReportOptions"/>.
/// </summary>
public sealed class ReportDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDescriptor"/> class.
    /// </summary>
    /// <param name="name">The stable technical name of the report.</param>
    /// <param name="displayName">The label shown to users.</param>
    /// <param name="description">The description shown to users.</param>
    /// <param name="category">The category the report is grouped under.</param>
    /// <param name="permission">The permission required to view and export the report.</param>
    /// <param name="type">The class that runs the report.</param>
    /// <param name="factory">Creates the report from the services of the scope when it is run.</param>
    public ReportDescriptor(
        string name,
        LocalizationSource displayName,
        LocalizationSource description,
        string category,
        Permission permission,
        Type type,
        Func<IServiceProvider, IReportRunner> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(factory);

        Name = name;
        DisplayName = displayName;
        Description = description;
        Category = category ?? string.Empty;
        Permission = permission;
        Type = type;
        Factory = factory;
    }

    /// <summary>
    /// Gets the stable technical name of the report.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the label shown to users, to localize with <c>IStringLocalizerFactory.Localize</c>.
    /// </summary>
    public LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the description shown to users, to localize with <c>IStringLocalizerFactory.Localize</c>.
    /// </summary>
    public LocalizationSource Description { get; }

    /// <summary>
    /// Gets the category the report is grouped under, or an empty string for none.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Gets the permission required to view and export the report.
    /// </summary>
    public Permission Permission { get; }

    /// <summary>
    /// Gets the class that runs the report.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the function that creates the report from the services of the scope.
    /// </summary>
    public Func<IServiceProvider, IReportRunner> Factory { get; }

    /// <summary>
    /// Describes a report from the static members of its class. The report is resolved from the services.
    /// </summary>
    /// <typeparam name="TReport">The class of the report.</typeparam>
    /// <returns>The descriptor.</returns>
    public static ReportDescriptor Create<TReport>()
        where TReport : class, IReport
    {
        return new ReportDescriptor(
            TReport.Name,
            TReport.DisplayName,
            TReport.Description,
            TReport.Category,
            TReport.Permission,
            typeof(TReport),
            static services => services.GetRequiredService<TReport>());
    }
}

/// <summary>
/// Holds the registered reports. <c>services.AddReport&lt;TReport&gt;()</c> adds them.
/// </summary>
public sealed class ReportOptions
{
    private readonly List<ReportDescriptor> _reports = [];

    /// <summary>
    /// Gets the registered reports, in the order they were registered.
    /// </summary>
    public IReadOnlyList<ReportDescriptor> Reports => _reports;

    /// <summary>
    /// Adds a report, unless the same class already runs a report of that name.
    /// </summary>
    /// <param name="descriptor">The descriptor of the report.</param>
    /// <returns>The options.</returns>
    public ReportOptions Add(ReportDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!_reports.Any(report => report.Type == descriptor.Type && string.Equals(report.Name, descriptor.Name, StringComparison.Ordinal)))
        {
            _reports.Add(descriptor);
        }

        return this;
    }
}
