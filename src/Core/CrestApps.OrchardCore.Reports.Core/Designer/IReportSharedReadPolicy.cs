namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Implemented by a data source whose data sets decide which rows to release from the conditions of the read, such as
/// a data set that holds back small groups until enough rows pass the viewer's filters. <see cref="ReportSharedReads"/>
/// normally serves a query from a kept read whose conditions are a subset of the query's, and the engine filters the
/// rows again; for such a data set that narrower view was never checked by the data set itself. A data set for which
/// <see cref="ServesNarrowerReads"/> returns <see langword="false"/> is served from a kept read only when the conditions
/// are the same, so each distinct set of conditions reaches the data set.
/// </summary>
public interface IReportSharedReadPolicy
{
    /// <summary>
    /// Gets whether a kept read of the data set may serve a query with more conditions than the read had.
    /// </summary>
    /// <param name="dataSet">The technical name of the data set.</param>
    /// <returns><see langword="true"/> to let a wider read serve a narrower query; <see langword="false"/> to share
    /// reads only between queries with the same conditions.</returns>
    bool ServesNarrowerReads(string dataSet);
}
