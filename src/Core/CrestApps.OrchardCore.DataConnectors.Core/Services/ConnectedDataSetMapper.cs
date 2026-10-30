using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Maps the rows a connector returns, keyed by source field names, to the fields of a data set: picks the fields
/// asked for, coerces each value to its field's type, and re-batches the rows.
/// </summary>
internal sealed class ConnectedDataSetMapper
{
    private readonly ConnectedDataField[] _fields;

    private ConnectedDataSetMapper(ConnectedDataField[] fields, bool identifiersAreKeyFilterable)
    {
        _fields = fields;

        var singleIdentifier = fields.Count(field => field.IsIdentifier) == 1;

        Fields = fields
            .Select(field => new DataField(field.Name, string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName, field.Type)
            {
                Description = field.Description,
                IsIdentifier = field.IsIdentifier,
                IsKeyFilterable = identifiersAreKeyFilterable && singleIdentifier && field.IsIdentifier,
            })
            .ToArray();
        SourceNames = fields.Select(field => field.SourceName).ToArray();
    }

    /// <summary>
    /// Gets the data set fields the rows hold, in order.
    /// </summary>
    public IReadOnlyList<DataField> Fields { get; }

    /// <summary>
    /// Gets the source field names read, in the order of <see cref="Fields"/>.
    /// </summary>
    public IReadOnlyList<string> SourceNames { get; }

    /// <summary>
    /// Gets the data set fields read, in the order of <see cref="Fields"/>.
    /// </summary>
    public IReadOnlyList<ConnectedDataField> DataSetFields => _fields;

    /// <summary>
    /// Creates a mapper for the fields a consumer asks for.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="requested">The data set field names asked for; empty or <see langword="null"/> for every included field.</param>
    /// <param name="allFields">Whether to read the fields consumers don't see too, as an import does.</param>
    /// <param name="identifiersAreKeyFilterable">Whether a single identifier can be filtered by key at the source.</param>
    /// <returns>The mapper.</returns>
    public static ConnectedDataSetMapper Create(
        ConnectedDataSet dataSet,
        ICollection<string> requested,
        bool allFields = false,
        bool identifiersAreKeyFilterable = false)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var fields = (dataSet.Fields ?? [])
            .Where(field => !string.IsNullOrEmpty(field.Name) && !string.IsNullOrEmpty(field.SourceName))
            .Where(field => allFields || field.IsIncluded)
            .Where(field => allFields || requested is null || requested.Count == 0 || requested.Contains(field.Name))
            .ToArray();

        return new ConnectedDataSetMapper(fields, identifiersAreKeyFilterable);
    }

    /// <summary>
    /// Translates a consumer's conditions on data set fields to conditions on source fields. A condition is kept only
    /// when the field reads the source's own type, so applying it at the source compares like with like.
    /// </summary>
    /// <param name="conditions">The consumer's conditions.</param>
    /// <returns>The conditions on source field names.</returns>
    public IEnumerable<DataCondition> ToSourceConditions(IEnumerable<DataCondition> conditions)
    {
        foreach (var condition in conditions ?? [])
        {
            if (condition is null)
            {
                continue;
            }

            var field = _fields.FirstOrDefault(entry => string.Equals(entry.Name, condition.Field, StringComparison.Ordinal));

            if (field is null || field.Type != field.SourceType)
            {
                continue;
            }

            yield return new DataCondition
            {
                Field = field.SourceName,
                Operator = condition.Operator,
                Values = [.. condition.Values ?? []],
                IsJoinKey = condition.IsJoinKey,
            };
        }
    }

    /// <summary>
    /// Maps the batches of a connector to the data set's fields.
    /// </summary>
    /// <param name="source">The connector's batches.</param>
    /// <param name="batchSize">The rows per batch.</param>
    /// <param name="maxRows">The most rows, or zero for no limit.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The mapped batches; an empty data set yields one empty batch, so its fields are known.</returns>
    public async IAsyncEnumerable<DataBatch> MapAsync(
        IAsyncEnumerable<DataBatch> source,
        int batchSize,
        int maxRows,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        batchSize = batchSize > 0 ? batchSize : DataSourceQuery.DefaultBatchSize;

        var remaining = maxRows > 0 ? maxRows : long.MaxValue;
        var rows = new List<object[]>(batchSize);
        var any = false;
        IReadOnlyList<DataField> lastFields = null;
        int[] indexes = null;

        await foreach (var batch in source.WithCancellation(cancellationToken))
        {
            if (!ReferenceEquals(batch.Fields, lastFields))
            {
                lastFields = batch.Fields;
                indexes = SourceNames.Select(batch.IndexOf).ToArray();
            }

            foreach (var sourceRow in batch.Rows)
            {
                if (remaining-- <= 0)
                {
                    break;
                }

                rows.Add(MapRow(sourceRow, indexes));

                if (rows.Count == batchSize)
                {
                    any = true;

                    yield return new DataBatch(Fields, rows);

                    rows = new List<object[]>(batchSize);
                }
            }

            if (remaining <= 0)
            {
                break;
            }
        }

        if (rows.Count > 0 || !any)
        {
            yield return new DataBatch(Fields, rows);
        }
    }

    /// <summary>
    /// Coerces one value to a field's type, normalizing dates to UTC.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <param name="type">The field's type.</param>
    /// <returns>The coerced value.</returns>
    public static object Coerce(object value, DataFieldType type)
    {
        value = value switch
        {
            DateTimeOffset date => date.UtcDateTime,
            DateTime { Kind: DateTimeKind.Unspecified } date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            DateTime { Kind: DateTimeKind.Local } date => date.ToUniversalTime(),
            Guid guid => guid.ToString(),
            byte[] bytes => Convert.ToBase64String(bytes),
            DBNull => null,
            _ => value,
        };

        return DataValues.Coerce(value, type);
    }

    private object[] MapRow(object[] sourceRow, int[] indexes)
    {
        var row = new object[_fields.Length];

        for (var index = 0; index < _fields.Length; index++)
        {
            var sourceIndex = indexes[index];

            row[index] = sourceIndex >= 0 && sourceIndex < sourceRow.Length
                ? Coerce(sourceRow[sourceIndex], _fields[index].Type)
                : null;
        }

        return row;
    }
}
