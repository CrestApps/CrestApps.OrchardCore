namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Describes one data set a report data source exposes, such as a content type, a database table, or a search
/// index.
/// </summary>
public sealed class ReportDataSetDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDataSetDescriptor"/> class.
    /// </summary>
    public ReportDataSetDescriptor()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDataSetDescriptor"/> class.
    /// </summary>
    /// <param name="name">The stable technical name of the data set.</param>
    /// <param name="displayName">The label shown in the designer.</param>
    /// <param name="description">The optional description shown in the designer.</param>
    public ReportDataSetDescriptor(string name, string displayName, string description = null)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// Gets or sets the stable technical name of the data set. Report designs store this name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the designer.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the optional description shown in the designer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the optional group the data set is listed under in the designer.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the name of the data set's main date field, such as when a record was created, if it has one. The
    /// builder filters a new report on it (the last 30 days by default), and data sources read only that range, so a
    /// report over a large data set reads what it shows instead of the newest rows up to the row limit.
    /// </summary>
    public string DefaultDateField { get; set; }

    /// <summary>
    /// Gets or sets the data sets this data set links to, when the data source can tell without reading data (for
    /// example from content definitions). The report builder uses them to suggest related data sets.
    /// </summary>
    public IList<ReportFieldReference> References { get; set; } = [];
}
