namespace CrestApps.OrchardCore.Workforce.Reports;

/// <summary>
/// A row of a Workforce data set: the record, with its worker's and team's names.
/// </summary>
/// <typeparam name="T">The record's type.</typeparam>
public sealed class WorkforceReportRow<T>
{
    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public T Record { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }
}
