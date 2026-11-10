using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Storage;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Locking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DataSources;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Refreshes imported data sets. A refresh writes a new version of the rows beside the published one and publishes it
/// only when every row was written, so a refresh that fails or is canceled leaves consumers reading the last good
/// rows.
/// </summary>
/// <remarks>
/// One refresh of a data set runs at a time across every instance of the site: a refresh that finds the data set
/// locked is recorded as skipped. The rows are written through their own connection, never inside the session's
/// transaction, and the session is saved before and after them.
/// </remarks>
internal sealed class DataSetRefreshService : IDataSetRefreshService
{
    private const string _lockArea = "dataconnectors.refresh";

    private readonly ISession _session;
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataSetRefreshRunStore _runStore;
    private readonly IDataSetRowStore _rowStore;
    private readonly ConnectedDataReader _reader;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly DataConnectorsOptions _options;
    private readonly ILogger _logger;

    public DataSetRefreshService(
        ISession session,
        IConnectedDataSetStore dataSetStore,
        IDataSetRefreshRunStore runStore,
        IDataSetRowStore rowStore,
        ConnectedDataReader reader,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<DataConnectorsOptions> options,
        ILogger<DataSetRefreshService> logger)
    {
        _session = session;
        _dataSetStore = dataSetStore;
        _runStore = runStore;
        _rowStore = rowStore;
        _reader = reader;
        _distributedLock = distributedLock;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DataSetRefreshRun> RefreshAsync(
        ConnectedDataSet dataSet,
        DataSetRefreshTrigger trigger,
        string requestedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var run = new DataSetRefreshRun
        {
            ItemId = IdGenerator.GenerateId(),
            DataSetId = dataSet.ItemId,
            Trigger = trigger,
            Mode = dataSet.Refresh?.Mode ?? DataSetRefreshMode.Full,
            Status = DataSetRefreshStatus.Running,
            StartedUtc = _clock.UtcNow,
            Version = IdGenerator.GenerateId(),
            RequestedBy = requestedBy,
        };

        var lockExpiration = _options.RefreshTimeout > TimeSpan.Zero
            ? _options.RefreshTimeout + TimeSpan.FromMinutes(5)
            : TimeSpan.FromHours(2);

        using var measurement = DataSetRefreshMeasurement.Start(dataSet, run);
        using var logScope = _logger.BeginRefreshScope(dataSet.ItemId.SanitizeLogValue(), run.ItemId);

        // A zero timeout never acquires the Redis lock, so the lock is tried once without waiting instead.
        var (locker, locked) = await _distributedLock.TryAcquireLockWithoutWaitingAsync($"CrestApps.DataConnectors.Refresh:{dataSet.ItemId}", lockExpiration, _lockArea);

        if (!locked)
        {
            // Another instance or request is refreshing the data set; it clears the request when it starts, and its
            // run is the one in the history.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("A refresh of the data set '{DataSet}' was skipped, because another refresh of it is running.", dataSet.Name);
            }

            run.Status = DataSetRefreshStatus.Skipped;
            run.CompletedUtc = run.StartedUtc;
            run.Error = "Another refresh of this data set was running.";
            measurement.End(CrestAppsOrchardCoreTelemetry.OutcomeSkipped);

            return run;
        }

        await using (locker)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.RefreshStarted(dataSet.Name.SanitizeLogValue(), DataConnectorsTelemetry.ToTag(trigger), DataConnectorsTelemetry.ToTag(run.Mode));
            }

            await _runStore.CreateAsync(run, cancellationToken);

            dataSet.Import ??= new DataSetImportState();
            dataSet.Import.RefreshRequestedUtc = null;
            await _dataSetStore.UpdateAsync(dataSet, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (_options.RefreshTimeout > TimeSpan.Zero)
            {
                timeout.CancelAfter(_options.RefreshTimeout);
            }

            try
            {
                await ImportAsync(dataSet, run, measurement, timeout.Token);
                await PublishAsync(dataSet, run, cancellationToken);
                measurement.End(CrestAppsOrchardCoreTelemetry.OutcomeOk);

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.RefreshSucceeded(dataSet.Name.SanitizeLogValue(), (long)measurement.Elapsed.TotalMilliseconds, DataConnectorsTelemetry.ToTag(run.Mode), run.RowsRead, run.RowCount);
                }
            }
            catch (Exception exception)
            {
                var canceled = exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
                var timedOut = exception is OperationCanceledException && !canceled;

                measurement.Fail(exception, timedOut);

                await FailAsync(dataSet, run, canceled ? DataSetRefreshStatus.Canceled : DataSetRefreshStatus.Failed, timedOut ? $"The refresh took longer than {_options.RefreshTimeout} and was stopped." : ToSafeMessage(exception));

                if (exception is DataConnectorException || timedOut || canceled)
                {
                    _logger.LogWarning("The refresh of the data set '{DataSet}' failed: {Error}", dataSet.Name, run.Error);
                }
                else
                {
                    _logger.LogError(exception, "The refresh of the data set '{DataSet}' failed.", dataSet.Name);
                }
            }

            await _runStore.PruneAsync(dataSet.ItemId, _options.RefreshRunsKept, CancellationToken.None);
            await _session.SaveChangesAsync(CancellationToken.None);
        }

        return run;
    }

    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken = default)
    {
        var due = await _dataSetStore.ListDueAsync(_clock.UtcNow, cancellationToken);
        var limit = Math.Max(1, _options.MaxRefreshesPerRun);
        var attempted = 0;
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var dataSet in due.Take(limit))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trigger = dataSet.Import?.RefreshRequestedUtc is null ? DataSetRefreshTrigger.Scheduled : DataSetRefreshTrigger.Manual;

            attempted++;

            try
            {
                var run = await RefreshAsync(dataSet, trigger, cancellationToken: cancellationToken);

                switch (run.Status)
                {
                    case DataSetRefreshStatus.Succeeded:
                        succeeded++;
                        break;

                    case DataSetRefreshStatus.Skipped:
                        skipped++;
                        break;

                    default:
                        failed++;
                        break;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The refresh couldn't record how it ended, such as when the database refused to save it. The run's
                // refreshes share one session, which YesSql cancels when a save fails, so the next refreshes couldn't
                // record theirs either: the run stops here without failing the task, and the next run, with a new
                // session, refreshes the data sets still due.
                failed++;
                _logger.RefreshAborted(exception, dataSet.ItemId.SanitizeLogValue());

                break;
            }
        }

        if (due.Count > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.RefreshSweepCompleted(due.Count, succeeded, failed, skipped, due.Count - attempted);
        }

        await SnapshotLastSuccessAsync(cancellationToken);

        // A refresh skipped because another refresh of the data set was running didn't run.
        return succeeded + failed;
    }

    private async Task SnapshotLastSuccessAsync(CancellationToken cancellationToken)
    {
        // The age gauge reads the snapshot, never the database, so it is taken here, once per run of the refresh task.
        if (!DataConnectorsTelemetry.LastSuccessAge.Enabled)
        {
            return;
        }

        try
        {
            DateTime? oldest = null;

            foreach (var dataSet in await _dataSetStore.ListAsync(cancellationToken))
            {
                if (!dataSet.IsEnabled || dataSet.AccessMode != DataSetAccessMode.Imported || dataSet.Refresh?.IntervalMinutes is not > 0)
                {
                    continue;
                }

                // A scheduled data set that never refreshed counts from when it was created.
                var lastSuccess = dataSet.Import?.LastSuccessUtc;

                if (lastSuccess is null && dataSet.CreatedUtc != default)
                {
                    lastSuccess = dataSet.CreatedUtc;
                }

                if (lastSuccess is { } value && (oldest is null || value < oldest))
                {
                    oldest = value;
                }
            }

            DataConnectorsTelemetry.SetOldestLastSuccess(ShellScope.Context?.Settings?.Name, oldest, _clock.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LastSuccessSnapshotFailed(exception);
        }
    }

    private async Task ImportAsync(
        ConnectedDataSet dataSet,
        DataSetRefreshRun run,
        DataSetRefreshMeasurement measurement,
        CancellationToken cancellationToken)
    {
        var source = await _reader.ResolveAsync(dataSet, cancellationToken);
        measurement.SetConnector(source.Descriptor.Name);

        // Finding the connection opened a transaction of the session. It is closed before any row is written, because
        // on SQLite an open read transaction keeps the row store's connection from committing.
        await _session.SaveChangesAsync(cancellationToken);

        if (!source.Descriptor.Supports(DataConnectorCapabilities.Import))
        {
            throw new DataConnectorException($"The connector '{source.Descriptor.Name}' can't import data sets.");
        }

        // The source's fields are read first, so a change is known before any row is written.
        IReadOnlyList<DataField> sourceFields;

        using (var describe = TelemetryScope.Start(DataConnectorsTelemetry.ActivitySource, "crestapps.dataconnector.refresh.describe", duration: null))
        {
            sourceFields = await _reader.DescribeAsync(source, dataSet, cancellationToken);
            describe.Succeed();
        }

        if (sourceFields.Count == 0)
        {
            throw new DataConnectorException("The source described no fields.");
        }

        var changes = ConnectedDataSetFields.Compare(dataSet.Fields, sourceFields);

        if (changes.HasChanges)
        {
            run.SchemaChanges = changes.ToString();
            measurement.Activity?.SetTag("crestapps.dataconnector.refresh.schema_changed", true);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.RefreshSchemaChanged(dataSet.Name.SanitizeLogValue(), run.SchemaChanges.SanitizeLogValue());
            }

            if (dataSet.Refresh?.SchemaChangePolicy == DataSetSchemaChangePolicy.Fail)
            {
                throw new DataConnectorException($"The source's fields changed, and this data set fails a refresh when they do. {run.SchemaChanges}.");
            }

            ConnectedDataSetFields.Merge(dataSet.Fields, sourceFields);
        }

        var mapper = ConnectedDataSetMapper.Create(dataSet, requested: null, allFields: true);
        var identifierIndexes = mapper.DataSetFields
            .Select((field, index) => (field, index))
            .Where(entry => entry.field.IsIdentifier)
            .Select(entry => entry.index)
            .ToArray();
        var incrementalField = FindIncrementalField(dataSet);
        var incrementalIndex = incrementalField is null ? -1 : Array.IndexOf([.. mapper.DataSetFields], incrementalField);

        // An incremental refresh needs a published version to start from, identifiers to replace rows by, and fields
        // that didn't change. Anything else reads every row.
        var incremental = run.Mode == DataSetRefreshMode.Incremental &&
            source.Descriptor.Supports(DataConnectorCapabilities.IncrementalImport) &&
            !string.IsNullOrEmpty(dataSet.Import?.ActiveVersion) &&
            identifierIndexes.Length > 0 &&
            incrementalIndex >= 0 &&
            !changes.HasChanges;

        if (run.Mode == DataSetRefreshMode.Incremental && !incremental)
        {
            var reason = GetFullReadReason(source, dataSet, identifierIndexes.Length, incrementalIndex, changes.HasChanges);
            measurement.Activity?.SetTag("crestapps.dataconnector.refresh.full_read_reason", reason);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.RefreshDowngradedToFull(dataSet.Name.SanitizeLogValue(), reason);
            }
        }

        run.Mode = incremental ? DataSetRefreshMode.Incremental : DataSetRefreshMode.Full;

        var context = new DataConnectorReadContext(source.Connection, source.Secrets, dataSet)
        {
            Purpose = DataConnectorReadPurpose.Import,
            BatchSize = Math.Max(1, _options.ImportBatchSize),
        };

        object largest = null;

        if (incremental)
        {
            largest = DataValues.Coerce(dataSet.Import.Watermark, incrementalField.Type);

            using (var copy = TelemetryScope.Start(DataConnectorsTelemetry.ActivitySource, "crestapps.dataconnector.refresh.copy", duration: null))
            {
                var copied = await _rowStore.CopyAsync(dataSet.ItemId, dataSet.Import.ActiveVersion, run.Version, cancellationToken);
                copy.SetSpanTag(DataConnectorsTelemetry.RowsSpanTag, copied);
                copy.Succeed();
            }

            if (largest is not null)
            {
                context.WatermarkField = incrementalField.SourceName;
                context.Watermark = DataValues.Coerce(largest, incrementalField.SourceType);
            }
        }

        // One span covers every batch, so a large import doesn't record thousands of them.
        using (var load = TelemetryScope.Start(DataConnectorsTelemetry.ActivitySource, "crestapps.dataconnector.refresh.batches", duration: null))
        {
            var batches = 0;

            await foreach (var batch in mapper.MapAsync(source.Connector.ReadAsync(context, cancellationToken), context.BatchSize, maxRows: 0, cancellationToken))
            {
                if (batch.Count == 0)
                {
                    continue;
                }

                var rows = new List<DataSetRow>(batch.Count);

                foreach (var row in batch.Rows)
                {
                    var key = identifierIndexes.Length == 0
                        ? null
                        : DataSetRows.ToKey([.. identifierIndexes.Select(index => row[index])]);

                    rows.Add(new DataSetRow(key, DataSetRows.ToJson(mapper.DataSetFields, row)));

                    if (incrementalIndex >= 0 && row[incrementalIndex] is { } value && !DataValues.IsEmpty(value) &&
                        (largest is null || DataValues.Compare(value, largest) > 0))
                    {
                        largest = value;
                    }
                }

                if (incremental)
                {
                    await _rowStore.DeleteKeysAsync(dataSet.ItemId, run.Version, [.. rows.Select(row => row.Key)], cancellationToken);
                }

                await _rowStore.WriteAsync(dataSet.ItemId, run.Version, rows, cancellationToken);
                run.RowsRead += rows.Count;
                batches++;
            }

            load.SetSpanTag("crestapps.dataconnector.batches", batches);
            load.SetSpanTag(DataConnectorsTelemetry.RowsSpanTag, run.RowsRead);
            load.Succeed();
        }

        using (var count = TelemetryScope.Start(DataConnectorsTelemetry.ActivitySource, "crestapps.dataconnector.refresh.count", duration: null))
        {
            run.RowCount = await _rowStore.CountAsync(dataSet.ItemId, run.Version, cancellationToken);
            count.SetSpanTag(DataConnectorsTelemetry.RowsSpanTag, run.RowCount);
            count.Succeed();
        }

        dataSet.Import.Watermark = largest switch
        {
            null => null,

            // A date keeps its time zone, so it reads back as the same instant.
            DateTime date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            _ => DataValues.ToText(largest),
        };
    }

    private async Task PublishAsync(ConnectedDataSet dataSet, DataSetRefreshRun run, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var import = dataSet.Import;

        import.PreviousVersion = import.ActiveVersion;
        import.ActiveVersion = run.Version;
        import.RowCount = run.RowCount;
        import.LastRefreshUtc = now;
        import.LastSuccessUtc = now;
        import.LastStatus = DataSetRefreshStatus.Succeeded;
        import.LastError = null;
        import.NextRefreshUtc = NextRefresh(dataSet, now);

        run.Status = DataSetRefreshStatus.Succeeded;
        run.CompletedUtc = now;

        await _dataSetStore.UpdateAsync(dataSet, cancellationToken);
        await _runStore.UpdateAsync(run, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        // The version published before stays for whoever is reading it; anything older, and the rows of refreshes
        // that failed without cleaning up, go.
        await _rowStore.DeleteAllAsync(dataSet.ItemId, [import.ActiveVersion, import.PreviousVersion], CancellationToken.None);
    }

    private async Task FailAsync(ConnectedDataSet dataSet, DataSetRefreshRun run, DataSetRefreshStatus status, string error)
    {
        var now = _clock.UtcNow;

        try
        {
            // The failure may have left a transaction of the session open, which on SQLite would keep the delete below
            // from committing.
            await _session.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The session couldn't be saved after a failed refresh of the data set '{DataSet}'.", dataSet.Name);
        }

        try
        {
            await _rowStore.DeleteVersionAsync(dataSet.ItemId, run.Version, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The staged rows are never read, and the next refresh removes them with every unpublished version.
            _logger.LogWarning(exception, "The rows of a failed refresh of the data set '{DataSet}' couldn't be deleted.", dataSet.Name);
        }

        run.Status = status;
        run.Error = error;
        run.CompletedUtc = now;

        dataSet.Import ??= new DataSetImportState();
        dataSet.Import.LastRefreshUtc = now;
        dataSet.Import.LastStatus = status;
        dataSet.Import.LastError = error;
        dataSet.Import.NextRefreshUtc = NextRefresh(dataSet, now);

        await _dataSetStore.UpdateAsync(dataSet, CancellationToken.None);
        await _runStore.UpdateAsync(run, CancellationToken.None);
        await _session.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Returns when the next scheduled refresh of a data set is due after a refresh ended.
    /// </summary>
    internal static DateTime? NextRefresh(ConnectedDataSet dataSet, DateTime utcNow)
        => dataSet.Refresh?.IntervalMinutes is > 0 and var minutes
            ? utcNow.AddMinutes(minutes)
            : null;

    // Why an incremental refresh reads every row: a bounded value for the span and the log.
    private static string GetFullReadReason(
        ConnectedSource source,
        ConnectedDataSet dataSet,
        int identifiers,
        int incrementalIndex,
        bool fieldsChanged)
    {
        if (!source.Descriptor.Supports(DataConnectorCapabilities.IncrementalImport))
        {
            return "unsupported";
        }

        if (string.IsNullOrEmpty(dataSet.Import?.ActiveVersion))
        {
            return "no_published_version";
        }

        if (identifiers == 0)
        {
            return "no_identifiers";
        }

        if (incrementalIndex < 0)
        {
            return "no_incremental_field";
        }

        return fieldsChanged ? "fields_changed" : CrestAppsOrchardCoreTelemetry.Other;
    }

    private static ConnectedDataField FindIncrementalField(ConnectedDataSet dataSet)
    {
        var name = dataSet.Refresh?.IncrementalField;

        return string.IsNullOrEmpty(name)
            ? null
            : dataSet.Fields.FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.Ordinal));
    }

    private static string ToSafeMessage(Exception exception)
    {
        // A connector's own errors are written to be shown. Anything else may hold a server's internals, so only its
        // kind is shown and the details go to the log.
        return exception switch
        {
            DataConnectorException => exception.Message,
            _ => $"The refresh failed with an unexpected {exception.GetType().Name}. See the site's log for details.",
        };
    }
}
