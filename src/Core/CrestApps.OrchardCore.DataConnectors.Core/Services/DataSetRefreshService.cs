using System.Globalization;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.DataSources;
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

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"CrestApps.DataConnectors.Refresh:{dataSet.ItemId}", TimeSpan.Zero, lockExpiration);

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

            return run;
        }

        await using (locker)
        {
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
                await ImportAsync(dataSet, run, timeout.Token);
                await PublishAsync(dataSet, run, cancellationToken);
            }
            catch (Exception exception)
            {
                var canceled = exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
                var timedOut = exception is OperationCanceledException && !canceled;

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
        var count = 0;

        foreach (var dataSet in due.Take(Math.Max(1, _options.MaxRefreshesPerRun)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trigger = dataSet.Import?.RefreshRequestedUtc is null ? DataSetRefreshTrigger.Scheduled : DataSetRefreshTrigger.Manual;

            await RefreshAsync(dataSet, trigger, cancellationToken: cancellationToken);
            count++;
        }

        return count;
    }

    private async Task ImportAsync(ConnectedDataSet dataSet, DataSetRefreshRun run, CancellationToken cancellationToken)
    {
        var source = await _reader.ResolveAsync(dataSet, cancellationToken);

        // Finding the connection opened a transaction of the session. It is closed before any row is written, because
        // on SQLite an open read transaction keeps the row store's connection from committing.
        await _session.SaveChangesAsync(cancellationToken);

        if (!source.Descriptor.Supports(DataConnectorCapabilities.Import))
        {
            throw new DataConnectorException($"The connector '{source.Descriptor.Name}' can't import data sets.");
        }

        // The source's fields are read first, so a change is known before any row is written.
        var sourceFields = await _reader.DescribeAsync(source, dataSet, cancellationToken);

        if (sourceFields.Count == 0)
        {
            throw new DataConnectorException("The source described no fields.");
        }

        var changes = ConnectedDataSetFields.Compare(dataSet.Fields, sourceFields);

        if (changes.HasChanges)
        {
            run.SchemaChanges = changes.ToString();

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

            await _rowStore.CopyAsync(dataSet.ItemId, dataSet.Import.ActiveVersion, run.Version, cancellationToken);

            if (largest is not null)
            {
                context.WatermarkField = incrementalField.SourceName;
                context.Watermark = DataValues.Coerce(largest, incrementalField.SourceType);
            }
        }

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
        }

        run.RowCount = await _rowStore.CountAsync(dataSet.ItemId, run.Version, cancellationToken);
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
