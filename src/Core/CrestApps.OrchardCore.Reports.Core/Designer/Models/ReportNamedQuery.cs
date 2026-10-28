namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A query a report runs besides its main query, so its visuals can show the data grouped another way: a table by
/// agent next to a chart by day. Each visual shows the columns of one query.
/// </summary>
public sealed class ReportNamedQuery
{
    /// <summary>
    /// Gets or sets the identifier of the query, unique within the report. Visuals refer to it with
    /// <see cref="ReportVisualDefinition.QueryId"/>.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name the designer shows for the query.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the query.
    /// </summary>
    public ReportQueryDefinition Query { get; set; } = new();
}
