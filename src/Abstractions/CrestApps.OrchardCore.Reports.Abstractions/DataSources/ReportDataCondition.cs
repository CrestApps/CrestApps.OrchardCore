namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// A filter condition the report engine offers to a data source so the source can narrow what it reads. The values
/// are already converted to the field's CLR type. Applying a condition is optional: the engine applies every filter
/// again after reading, so a source may ignore any condition it cannot translate.
/// </summary>
public sealed class ReportDataCondition
{
    /// <summary>
    /// Gets or sets the technical name of the data set field the condition applies to.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the comparison operator.
    /// </summary>
    public ReportFilterOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the comparison values, converted to the field's CLR type. An entry may be <see langword="null"/>
    /// for an open bound of <see cref="ReportFilterOperator.Between"/>.
    /// </summary>
    public IList<object> Values { get; set; } = [];
}
