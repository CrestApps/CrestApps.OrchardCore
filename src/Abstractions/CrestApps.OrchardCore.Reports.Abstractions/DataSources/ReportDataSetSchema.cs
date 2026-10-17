namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Describes the fields of one data set.
/// </summary>
public sealed class ReportDataSetSchema
{
    /// <summary>
    /// Gets or sets the data set the schema describes.
    /// </summary>
    public ReportDataSetDescriptor DataSet { get; set; }

    /// <summary>
    /// Gets or sets the fields of the data set, in the order the designer lists them.
    /// </summary>
    public IList<ReportFieldDescriptor> Fields { get; set; } = [];

    /// <summary>
    /// Finds a field by its technical name.
    /// </summary>
    /// <param name="name">The technical field name.</param>
    /// <returns>The field, or <see langword="null"/> when the data set has no such field.</returns>
    public ReportFieldDescriptor FindField(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }
}
