namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// One data set of a <see cref="ReportRecordDataSource"/>.
/// </summary>
public interface IReportRecordDataSet
{
    /// <summary>
    /// Gets the data set's descriptor.
    /// </summary>
    ReportDataSetDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the fields of the data set.
    /// </summary>
    IReadOnlyList<ReportFieldDescriptor> Fields { get; }

    /// <summary>
    /// Determines whether the principal of <paramref name="context"/> may read the data set.
    /// </summary>
    /// <param name="context">The context of the run.</param>
    /// <returns><see langword="true"/> when the principal may read it.</returns>
    Task<bool> CanReadAsync(ReportDataSourceContext context);

    /// <summary>
    /// Reads the rows of the data set. The caller has checked <see cref="CanReadAsync"/>.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// A field of a <see cref="ReportRecordDataSet{TRecord}"/>: what it is, and how to read it from a record.
/// </summary>
/// <typeparam name="TRecord">The record type.</typeparam>
public sealed class ReportRecordField<TRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRecordField{TRecord}"/> class.
    /// </summary>
    /// <param name="descriptor">The field descriptor.</param>
    /// <param name="read">Reads the field from a record.</param>
    public ReportRecordField(ReportFieldDescriptor descriptor, Func<TRecord, object> read)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(read);

        Descriptor = descriptor;
        Read = read;
    }

    /// <summary>
    /// Gets the field descriptor.
    /// </summary>
    public ReportFieldDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the function that reads the field from a record.
    /// </summary>
    public Func<TRecord, object> Read { get; }
}

/// <summary>
/// A data set whose rows are records of one type, such as the documents of a store: it declares its fields with the
/// function that reads each from a record, loads the records, and turns them into rows. Values are converted to the
/// type of their field, so a field can return, for example, an <see cref="int"/> for an
/// <see cref="ReportDataType.Integer"/> field or an enum for a <see cref="ReportDataType.Text"/> field.
/// </summary>
/// <typeparam name="TRecord">The record type.</typeparam>
public abstract class ReportRecordDataSet<TRecord> : IReportRecordDataSet
{
    private readonly List<ReportRecordField<TRecord>> _fields = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportRecordDataSet{TRecord}"/> class.
    /// </summary>
    /// <param name="descriptor">The data set's descriptor.</param>
    protected ReportRecordDataSet(ReportDataSetDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptor = descriptor;
    }

    /// <inheritdoc/>
    public ReportDataSetDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ReportFieldDescriptor> Fields => _fields.Select(recordField => recordField.Descriptor).ToArray();

    /// <inheritdoc/>
    public abstract Task<bool> CanReadAsync(ReportDataSourceContext context);

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var fields = _fields
            .Where(field => query.Fields is null || query.Fields.Count == 0 || query.Fields.Contains(field.Descriptor.Name))
            .ToList();
        var maxRows = Math.Max(1, query.MaxRows);
        var records = await LoadAsync(query, maxRows + 1, cancellationToken) ?? [];
        var table = new ReportDataTable
        {
            Fields = fields.Select(field => field.Descriptor).ToList(),
        };

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (record is null)
            {
                continue;
            }

            if (table.Rows.Count >= maxRows)
            {
                table.Truncated = true;

                break;
            }

            table.Rows.Add(fields.Select(field => ReportDataValues.Coerce(ToValue(field.Read(record)), field.Descriptor.DataType)).ToArray());
        }

        return table;
    }

    /// <summary>
    /// Loads at most <paramref name="take"/> records, newest first. It may use the query's conditions to read fewer
    /// records, but must never leave out a record that matches them: the report applies every condition again.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="take">The most records to load; one more than the report keeps, so it can tell it was cut.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The records.</returns>
    protected abstract Task<IEnumerable<TRecord>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a field.
    /// </summary>
    /// <param name="name">The stable field name.</param>
    /// <param name="displayName">The label shown in the builder.</param>
    /// <param name="dataType">The field type.</param>
    /// <param name="read">Reads the field from a record.</param>
    /// <param name="group">The optional group the field is listed under.</param>
    /// <param name="isIdentifier">Whether the field is a key other data sets can join on.</param>
    /// <param name="references">The data sets the field points to.</param>
    /// <returns>The field descriptor, to adjust further.</returns>
    protected ReportFieldDescriptor AddField(
        string name,
        string displayName,
        ReportDataType dataType,
        Func<TRecord, object> read,
        string group = null,
        bool isIdentifier = false,
        params ReportFieldReference[] references)
    {
        var descriptor = new ReportFieldDescriptor(name, displayName, dataType, group)
        {
            IsIdentifier = isIdentifier,
        };

        foreach (var reference in references ?? [])
        {
            descriptor.References.Add(reference);

            if (!Descriptor.References.Any(existing => existing.Source == reference.Source && existing.DataSet == reference.DataSet))
            {
                Descriptor.References.Add(reference);
            }
        }

        _fields.Add(new ReportRecordField<TRecord>(descriptor, read));

        return descriptor;
    }

    // Enums are reported by name, and date-times are kept in UTC.
    private static object ToValue(object value)
    {
        return value switch
        {
            Enum enumValue => enumValue.ToString(),
            DateTime { Kind: DateTimeKind.Unspecified } date => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            DateTimeOffset offset => offset.UtcDateTime,
            _ => value,
        };
    }
}
