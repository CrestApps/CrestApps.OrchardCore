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
}
