using System.Diagnostics;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.DataConnectors.Telemetry;

/// <summary>
/// Measures one refresh of an imported data set: the <c>crestapps.dataconnector.refresh</c> span, its duration, the
/// refreshes counter, the rows it read and the refreshes running now. The tags are recorded when it is disposed, so the
/// mode is the one the refresh ended with, after an incremental refresh fell back to a full one.
/// </summary>
internal sealed class DataSetRefreshMeasurement : IDisposable
{
    private readonly TelemetryScope _scope;
    private readonly DataSetRefreshRun _run;
    private readonly TagList _activeTags;
    private readonly bool _counted;
    private string _connector = CrestAppsOrchardCoreTelemetry.Unknown;
    private string _outcome;
    private bool _disposed;

    private DataSetRefreshMeasurement(
        TelemetryScope scope,
        DataSetRefreshRun run,
        TagList activeTags,
        bool counted)
    {
        _scope = scope;
        _run = run;
        _activeTags = activeTags;
        _counted = counted;
    }

    /// <summary>
    /// Gets the time elapsed since the refresh started.
    /// </summary>
    public TimeSpan Elapsed => _scope.Elapsed;

    /// <summary>
    /// Gets the span of the refresh, or <see langword="null"/> when nothing listens.
    /// </summary>
    public Activity Activity => _scope.Activity;

    /// <summary>
    /// Starts measuring a refresh.
    /// </summary>
    /// <param name="dataSet">The data set refreshed.</param>
    /// <param name="run">The refresh.</param>
    /// <returns>The measurement, to dispose when the refresh ends.</returns>
    public static DataSetRefreshMeasurement Start(ConnectedDataSet dataSet, DataSetRefreshRun run)
    {
        var trigger = DataConnectorsTelemetry.ToTag(run.Trigger);
        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger },
        };

        var scope = TelemetryScope.Start(DataConnectorsTelemetry.ActivitySource, "crestapps.dataconnector.refresh", DataConnectorsTelemetry.RefreshDuration, tags);
        scope.SetSpanTag(DataConnectorsTelemetry.DataSetIdSpanTag, dataSet.ItemId);
        scope.SetSpanTag(DataConnectorsTelemetry.RefreshIdSpanTag, run.ItemId);

        // The same tags go with the increment and the decrement, so the series returns to zero.
        var activeTags = tags;
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref activeTags);

        var counted = DataConnectorsTelemetry.ActiveRefreshes.Enabled;

        if (counted)
        {
            DataConnectorsTelemetry.ActiveRefreshes.Add(1, activeTags);
        }

        return new DataSetRefreshMeasurement(scope, run, activeTags, counted);
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
    /// Records how the refresh ended without an exception: <c>ok</c> or <c>skipped</c>.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    public void End(string outcome)
    {
        _outcome = outcome;
        _scope.SetOutcome(outcome);
    }

    /// <summary>
    /// Records that the refresh failed.
    /// </summary>
    /// <param name="exception">The exception the refresh failed with.</param>
    /// <param name="timedOut">Whether the refresh ran longer than the refresh timeout.</param>
    public void Fail(Exception exception, bool timedOut)
    {
        DataConnectorsTelemetry.Fail(_scope, exception);

        if (timedOut)
        {
            _scope.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeTimeout);
            _scope.Activity?.SetStatus(ActivityStatusCode.Error, "The refresh ran longer than the refresh timeout.");
        }

        _outcome = _scope.Outcome;
    }

    /// <summary>
    /// Records the refresh and ends its span.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var outcome = _outcome ?? CrestAppsOrchardCoreTelemetry.OutcomeError;
        var mode = DataConnectorsTelemetry.ToTag(_run.Mode);

        _scope.AddMetricTag(DataConnectorsTelemetry.ModeTag, mode);
        _scope.AddMetricTag(DataConnectorsTelemetry.ConnectorTag, _connector);
        _scope.SetSpanTag(DataConnectorsTelemetry.RowsSpanTag, _run.RowsRead);

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.TriggerTag, DataConnectorsTelemetry.ToTag(_run.Trigger) },
            { DataConnectorsTelemetry.ModeTag, mode },
            { DataConnectorsTelemetry.ConnectorTag, _connector },
        };

        if (outcome == CrestAppsOrchardCoreTelemetry.OutcomeOk)
        {
            DataConnectorsTelemetry.Record(DataConnectorsTelemetry.RefreshRows, _run.RowsRead, tags);
        }

        tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
        DataConnectorsTelemetry.Add(DataConnectorsTelemetry.Refreshes, tags);

        _scope.Dispose();

        // Only a refresh counted when it started is taken off, so a listener that attaches while refreshes run never
        // sees the count drop below zero.
        if (_counted)
        {
            DataConnectorsTelemetry.ActiveRefreshes.Add(-1, _activeTags);
        }
    }
}
