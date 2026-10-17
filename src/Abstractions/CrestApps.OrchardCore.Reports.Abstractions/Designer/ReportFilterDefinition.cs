using CrestApps.OrchardCore.Reports.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A filter of a designed query. A fixed filter always applies with its <see cref="Values"/>; an exposed filter is
/// rendered for the people who run the report, who can change its values.
/// </summary>
public sealed class ReportFilterDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the filter, unique within the query. Exposed filter values are submitted under
    /// this identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the filter applies to: a field key (<c>alias.Field</c> or a calculated field name) when
    /// <see cref="Stage"/> is <see cref="ReportFilterStage.Rows"/>, or a column identifier when it is
    /// <see cref="ReportFilterStage.Result"/>.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets whether the filter applies to rows before grouping or to the result after grouping.
    /// </summary>
    public ReportFilterStage Stage { get; set; }

    /// <summary>
    /// Gets or sets the comparison operator.
    /// </summary>
    public ReportFilterOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the comparison values, written with the invariant culture (dates as <c>yyyy-MM-dd</c> or
    /// <c>yyyy-MM-ddTHH:mm</c> in the tenant time zone). For an exposed filter these are the default values.
    /// </summary>
    public IList<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the filter is rendered for the people who run the report.
    /// </summary>
    public bool Exposed { get; set; }

    /// <summary>
    /// Gets or sets the label of an exposed filter.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the control used to render an exposed filter.
    /// </summary>
    public ReportFilterControl Control { get; set; }
}
