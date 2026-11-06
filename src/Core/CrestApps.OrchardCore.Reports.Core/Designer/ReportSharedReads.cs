using System.Runtime.CompilerServices;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Shares the reads of a data set between the queries of one report run. A dashboard often has several queries over
/// the same data set with the same filters, each a copy of the others grouped another way: the first query that reads
/// the data set keeps its rows, and the others read them from memory instead of from the data source. A query whose
/// conditions include all of those of a kept read, such as one a click filters, reads the kept rows too, since the
/// engine applies every filter to the rows again. Each data set is asked for every field the queries read from it, so one
/// read serves them all. Reads larger than <see cref="MaxRows"/> rows are not kept.
/// </summary>
public sealed class ReportSharedReads
{
    private readonly Dictionary<string, HashSet<string>> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<KeptRead>> _reads = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSharedReads"/> class.
    /// </summary>
    /// <param name="maxRows">The most rows a kept read holds.</param>
    public ReportSharedReads(int maxRows)
    {
        MaxRows = Math.Max(0, maxRows);
    }

    /// <summary>
    /// Gets the most rows a kept read holds.
    /// </summary>
    public int MaxRows { get; }

    /// <summary>
    /// Gets the number of reads served from memory instead of from a data source.
    /// </summary>
    public int SharedCount { get; private set; }

    /// <summary>
    /// Registers the data sets a planned query reads and the fields it reads from them. A data set is kept only when two
    /// or more queries of the run read it.
    /// </summary>
    /// <param name="plan">The plan of a query of the run.</param>
    public void Register(ReportQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        lock (_lock)
        {
            foreach (var dataSet in plan.DataSets)
            {
                var key = DataSetKey(dataSet);

                if (!_fields.TryGetValue(key, out var fields))
                {
                    _fields[key] = fields = new HashSet<string>(StringComparer.Ordinal);
                }
                else
                {
                    // Read by a second query: kept from now on.
                    _reads.TryAdd(key, []);
                }

                fields.UnionWith(dataSet.UsedFields.Keys);
            }
        }
    }

    /// <summary>
    /// Reads a data set, from the rows kept by an earlier read of the run when they hold what the query asks for, or from
    /// its data source, keeping the rows when other queries of the run read the same data set.
    /// </summary>
    /// <param name="dataSet">The planned data set.</param>
    /// <param name="query">The query to send to the data source.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The batches of rows.</returns>
    public IAsyncEnumerable<DataBatch> ReadAsync(PlannedDataSet dataSet, DataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(query);

        var key = DataSetKey(dataSet);
        List<KeptRead> kept;

        lock (_lock)
        {
            if (MaxRows == 0 || query.Conditions.Any(condition => condition.IsJoinKey) || !_reads.TryGetValue(key, out kept))
            {
                return dataSet.Source.ReadAsync(query, cancellationToken);
            }

            // Every field any query of the run reads from the data set, so one read serves them all.
            query.Fields = new HashSet<string>(_fields[key], StringComparer.Ordinal);

            var conditions = Describe(query.Conditions);
            var match = kept.FirstOrDefault(read => read.Serves(conditions, query.MaxRows));

            if (match is not null)
            {
                SharedCount++;

                return Replay(match, query.MaxRows, cancellationToken);
            }
        }

        return ReadAndKeepAsync(dataSet, query, key, cancellationToken);
    }

    private async IAsyncEnumerable<DataBatch> ReadAndKeepAsync(
        PlannedDataSet dataSet,
        DataSourceQuery query,
        string key,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var batches = new List<DataBatch>();
        var rows = 0;
        var keeping = true;

        await foreach (var batch in dataSet.Source.ReadAsync(query, cancellationToken).WithCancellation(cancellationToken))
        {
            if (keeping)
            {
                rows += batch.Rows.Count;

                if (rows > MaxRows)
                {
                    keeping = false;
                    batches = null;
                }
                else
                {
                    // A copy, so a source that reuses its buffers can't change the rows kept.
                    batches.Add(new DataBatch(batch.Fields, batch.Rows.Select(row => (object[])row.Clone()).ToArray()));
                }
            }

            yield return batch;
        }

        // Only a read that reached its end is kept: one stopped early doesn't hold every row.
        if (keeping)
        {
            lock (_lock)
            {
                _reads[key].Add(new KeptRead(Describe(query.Conditions), batches, rows, query.MaxRows));
            }
        }
    }

    private static async IAsyncEnumerable<DataBatch> Replay(KeptRead read, int maxRows, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var left = maxRows > 0 ? maxRows : int.MaxValue;

        foreach (var batch in read.Batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (left <= 0)
            {
                yield break;
            }

            if (batch.Rows.Count <= left)
            {
                left -= batch.Rows.Count;

                yield return batch;
            }
            else
            {
                yield return new DataBatch(batch.Fields, batch.Rows.Take(left).ToArray());

                yield break;
            }

            await Task.Yield();
        }
    }

    private static string DataSetKey(PlannedDataSet dataSet)
    {
        return dataSet.Reference.Source + "\u001F" + dataSet.Reference.DataSet;
    }

    // The conditions of a read, as text that tells them apart.
    private static HashSet<string> Describe(IEnumerable<DataCondition> conditions)
    {
        return conditions
            .Select(condition => $"{condition.Field}\u001E{condition.Operator}\u001E{string.Join('\u001E', (condition.Values ?? []).Select(DataValues.ToText))}")
            .ToHashSet(StringComparer.Ordinal);
    }

    // A read kept in memory: its conditions and its rows, and whether it holds every row its conditions keep.
    private sealed class KeptRead
    {
        public KeptRead(HashSet<string> conditions, List<DataBatch> batches, int rows, int maxRows)
        {
            Conditions = conditions;
            Batches = batches;
            IsComplete = maxRows <= 0 || rows < maxRows;
            Rows = rows;
            MaxRows = maxRows;
        }

        public HashSet<string> Conditions { get; }

        public List<DataBatch> Batches { get; }

        public int Rows { get; }

        public int MaxRows { get; }

        // Whether the read stopped before its row limit, so it holds every row its conditions keep.
        public bool IsComplete { get; }

        // A read serves a query with the same conditions and row limit. When it holds every row its conditions keep, it
        // also serves a query with all of its conditions and more, whose rows the engine filters again, if they fit in
        // that query's row limit: the engine counts the rows read against the limit before it filters them.
        public bool Serves(HashSet<string> conditions, int maxRows)
        {
            if (Conditions.SetEquals(conditions) && (MaxRows == maxRows || IsComplete))
            {
                return true;
            }

            return IsComplete && Conditions.IsSubsetOf(conditions) && (maxRows <= 0 || Rows < maxRows);
        }
    }
}
