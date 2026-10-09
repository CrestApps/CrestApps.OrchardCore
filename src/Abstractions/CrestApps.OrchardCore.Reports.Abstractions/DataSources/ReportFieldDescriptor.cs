namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Describes one field of a report data set: its stable technical name, the label shown in the designer, and its
/// data type.
/// </summary>
public sealed class ReportFieldDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportFieldDescriptor"/> class.
    /// </summary>
    public ReportFieldDescriptor()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportFieldDescriptor"/> class.
    /// </summary>
    /// <param name="name">The stable technical name of the field.</param>
    /// <param name="displayName">The label shown in the designer.</param>
    /// <param name="dataType">The data type of the field values.</param>
    /// <param name="group">The optional group the field is listed under in the designer.</param>
    public ReportFieldDescriptor(string name, string displayName, ReportDataType dataType, string group = null)
    {
        Name = name;
        DisplayName = displayName;
        DataType = dataType;
        Group = group;
    }

    /// <summary>
    /// Gets or sets the stable technical name of the field. Report designs store this name, so it must not change
    /// between releases. It may contain dots (for example <c>Customer.Email</c>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the designer.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the data type of the field values.
    /// </summary>
    public ReportDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the optional group the field is listed under in the designer (for example a content part name).
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets an optional description shown as a hint in the designer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field identifies a record, such as a primary or foreign key. The
    /// designer suggests identifier fields when the user joins data sets.
    /// </summary>
    public bool IsIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the data sets whose records this field's values identify. A field may reference several data sets,
    /// such as a picker that accepts more than one content type.
    /// </summary>
    public IList<ReportFieldReference> References { get; set; } = [];
}
