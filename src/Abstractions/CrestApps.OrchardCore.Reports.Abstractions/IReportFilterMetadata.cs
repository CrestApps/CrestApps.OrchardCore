namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Describes the named filter dimensions supported by a report.
/// </summary>
public interface IReportFilterMetadata
{
    /// <summary>
    /// Gets the stable filter names supported by the report.
    /// </summary>
    IReadOnlyCollection<string> FilterNames { get; }
}
