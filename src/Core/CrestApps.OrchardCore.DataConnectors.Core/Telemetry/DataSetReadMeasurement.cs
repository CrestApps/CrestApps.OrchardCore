using System.Diagnostics;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.DataConnectors.Telemetry;

/// <summary>
/// Measures one read of a data set from its source: the <c>crestapps.dataconnector.read</c> span, its duration, the
/// rows it returned, and its failures and timeouts.
/// </summary>
/// <remarks>
/// A read the consumer stops early, such as a preview that has the rows it needs, still succeeded; only an exception
/// marks it failed.
/// </remarks>
internal sealed class DataSetReadMeasurement : IDisposable
{
    private readonly TelemetryScope _scope;
    private readonly string _purpose;
    private string _connector = CrestAppsOrchardCoreTelemetry.Unknown;
    private long _rows;
    private bool _disposed;

    private DataSetReadMeasurement(TelemetryScope scope, string purpose)
    {
        _scope = scope;
        _purpose = purpose;
    }

    /// <summary>
    /// Starts measuring a read.
    /// </summary>
    /// <param name="dataSet">The data set read.</param>
    /// <param name="purpose">Why the rows are read.</param>
    /// <returns>The measurement, to dispose when the read ends.</returns>
    public static DataSetReadMeasurement Start(ConnectedDataSet dataSet, DataConnectorReadPurpose purpose)
    {
        var purposeTag = DataConnectorsTelemetry.ToTag(purpose);
        var scope = TelemetryScope.Start(
            DataConnectorsTelemetry.ActivitySource,
            "crestapps.dataconnector.read",
            DataConnectorsTelemetry.ReadDuration,
            new TagList
            {
                { DataConnectorsTelemetry.PurposeTag, purposeTag },
            });

        scope.SetSpanTag(DataConnectorsTelemetry.DataSetIdSpanTag, dataSet.ItemId);
        scope.SetSpanTag(DataConnectorsTelemetry.ConnectionIdSpanTag, dataSet.ConnectionId);
        scope.Succeed();

        return new DataSetReadMeasurement(scope, purposeTag);
    }

    /// <summary>
    /// Records the connector that reads the data set, once its connection is resolved.
    /// </summary>
    /// <param name="connector">The name of the registered connector.</param>
    public void SetConnector(string connector)
    {
        _connector = string.IsNullOrEmpty(connector) ? CrestAppsOrchardCoreTelemetry.Unknown : connector;
    }

    /// <summary>
    /// Adds the rows of a batch the read returned.
    /// </summary>
    /// <param name="count">The number of rows.</param>
    public void AddRows(int count)
    {
        _rows += count;
    }

    /// <summary>
    /// Records that the read failed.
    /// </summary>
    /// <param name="exception">The exception the read failed with.</param>
    /// <param name="timedOut">Whether the read ran longer than the live read timeout.</param>
    public void Fail(Exception exception, bool timedOut)
    {
        DataConnectorsTelemetry.Fail(_scope, exception);

        var tags = new TagList
        {
            { DataConnectorsTelemetry.ConnectorTag, _connector },
            { DataConnectorsTelemetry.PurposeTag, _purpose },
        };

        if (timedOut)
        {
            _scope.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeTimeout);
            _scope.Activity?.SetStatus(ActivityStatusCode.Error, "The read ran longer than the live read timeout.");
            DataConnectorsTelemetry.Add(DataConnectorsTelemetry.ReadTimeouts, tags);

            return;
        }

        // The consumer stopped the read; that isn't the source failing.
        if (_scope.Outcome == CrestAppsOrchardCoreTelemetry.OutcomeCancelled)
        {
            return;
        }

        tags.Add(DataConnectorsTelemetry.TransientTag, exception is DataConnectorException { IsTransient: true });
        DataConnectorsTelemetry.Add(DataConnectorsTelemetry.ReadFailures, tags);
    }

    /// <summary>
    /// Records the read and ends its span.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _scope.AddMetricTag(DataConnectorsTelemetry.ConnectorTag, _connector);
        _scope.SetSpanTag(DataConnectorsTelemetry.RowsSpanTag, _rows);

        if (_scope.Outcome == CrestAppsOrchardCoreTelemetry.OutcomeOk)
        {
            DataConnectorsTelemetry.Record(DataConnectorsTelemetry.ReadRows, _rows, new TagList
            {
                { DataConnectorsTelemetry.ConnectorTag, _connector },
                { DataConnectorsTelemetry.PurposeTag, _purpose },
            });
        }

        _scope.Dispose();
    }
}
